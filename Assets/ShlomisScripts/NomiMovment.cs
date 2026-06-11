using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class NomiMovment : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody2D player;
    public Animator animator;

    [Header("Movement")]
    public float speed = 10f;
    public InputActionReference move;

    [Header("Jump")]
    public InputActionReference jump;
    [SerializeField] public float jumpForce = 5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Slope")]
    [SerializeField] private float slopeCheckDistance = 0.6f;
    [SerializeField] private float minSlopeAngle = 1f;
    private bool isOnSlope;
    private float normalGravityScale;
    private bool isJumping;

    private Vector2 moveDirection;
    private readonly Collider2D[] groundHitsForAudio = new Collider2D[8];
    private Collider2D lastGroundColliderForAudio;
    private Collider2D[] playerColliders;
    private Vector2 platformCarryVelocity;
    private float platformCarryUntilTime;

    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private NomiFootstepAudio footstepAudio;

    private Transform lastFollowTarget;

    public static NomiMovment instance;

    public Transform GroundCheck => groundCheck;
    public float GroundCheckRadius => groundCheckRadius;
    public LayerMask GroundLayer => groundLayer;
    public Collider2D[] SolidColliders
    {
        get
        {
            if (playerColliders == null || playerColliders.Length == 0)
                CachePlayerColliders();

            return playerColliders;
        }
    }

    public bool IsGroundedForAudio => IsGrounded();
    public bool IsPlayerControlledForAudio => IsPlayerControlled();
    public float HorizontalInputForAudio => IsPlayerControlled() ? moveDirection.x : 0f;
    public float HorizontalSpeedForAudio => player != null ? Mathf.Abs(player.linearVelocity.x) : 0f;
    public Collider2D GroundColliderForAudio => GetGroundCollider();
    public Collider2D LastGroundColliderForAudio => lastGroundColliderForAudio;

    public bool HasGroundColliderForAudio(Collider2D target)
    {
        return IsStandingOn(target);
    }

    private void Awake()
    {
        CachePlayerColliders();
    }

    public bool IsStandingOn(Collider2D target)
    {
        if (target == null || groundCheck == null) return false;

        var hitCount = Physics2D.OverlapCircleNonAlloc(
            groundCheck.position,
            groundCheckRadius,
            groundHitsForAudio,
            groundLayer);

        for (var i = 0; i < hitCount; i++)
            if (groundHitsForAudio[i] == target) return true;

        return false;
    }

    void Start()
    {
        instance = this;
        FindAudioReferences();
        CachePlayerColliders();

        if (playerCamera != null)
            lastFollowTarget = playerCamera.Follow;

        normalGravityScale = player.gravityScale;
    }

    public void MoveWithPlatform(Vector2 targetPosition, float deltaTime)
    {
        if (player == null) return;

        Vector2 delta = targetPosition - player.position;
        platformCarryVelocity = deltaTime > 0f ? delta / deltaTime : Vector2.zero;
        platformCarryUntilTime = Time.fixedTime + Mathf.Max(Time.fixedDeltaTime * 2f, 0.02f);

        player.gravityScale = 0f;
        player.linearVelocity = platformCarryVelocity;
        player.MovePosition(targetPosition);
    }

    public void ClearPlatformCarry()
    {
        platformCarryVelocity = Vector2.zero;
        platformCarryUntilTime = 0f;
    }

    void Update()
    {
        CheckCameraTarget();

        if (!IsPlayerControlled()) return;

        moveDirection = ReadMoveInput();
        HandleFlip();
        AnimationHandler();
        CheckSlope();
    }

    private void CheckSlope()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);

        if (hit)
        {
            float angle = Vector2.Angle(hit.normal, Vector2.up);
            isOnSlope = angle > minSlopeAngle;
        }
        else
        {
            isOnSlope = false;
        }
    }

    private void CheckCameraTarget()
    {
        if (playerCamera == null) return;

        Transform currentTarget = playerCamera.Follow;
        if (currentTarget == lastFollowTarget) return;

        lastFollowTarget = currentTarget;

        var composer = playerCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null) return;

        if (currentTarget == this.transform)
            composer.TargetOffset = new Vector3(composer.TargetOffset.x, 3f, composer.TargetOffset.z);
        else
            composer.TargetOffset = new Vector3(composer.TargetOffset.x, 0f, composer.TargetOffset.z);
    }

    private bool IsPlayerControlled()
    {
        if (playerCamera == null) return true;
        return playerCamera.Follow == this.transform;
    }

    private void OnEnable()  => SubscribeAction(jump, Jump);
    private void OnDisable() => UnsubscribeAction(jump, Jump);

    private void Jump(InputAction.CallbackContext _)
    {
        if (!IsPlayerControlled()) return;

        if (IsGrounded())
        {
            player.gravityScale = normalGravityScale;
            isJumping = true;

            player.linearVelocity = new Vector2(player.linearVelocity.x, 0f);
            player.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            animator.SetTrigger("Jump");
            FindAudioReferences();
            footstepAudio?.PlayJumpFromInput();
        }
    }

    private void FindAudioReferences()
    {
        if (footstepAudio == null)
            footstepAudio = GetComponent<NomiFootstepAudio>();
    }

    private bool IsGrounded()
    {
        return groundCheck != null &&
               Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private Collider2D GetGroundCollider()
    {
        var groundCollider = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : null;

        if (groundCollider != null)
            lastGroundColliderForAudio = groundCollider;

        return groundCollider;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    public void AnimationHandler()
    {
        animator.SetBool("Grounded2", IsGrounded());
        animator.SetBool("IsFalling", IsFalling());
        animator.SetFloat("Speed", Mathf.Abs(moveDirection.x));
    }

    private bool IsFalling() => player.linearVelocity.y < -0.1f;

    private void HandleFlip()
    {
        if (moveDirection.x > 0f)
            transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        else if (moveDirection.x < 0f)
            transform.localScale = new Vector3(-0.85f, 0.85f, 0.85f);
    }

    void FixedUpdate()
    {
        if (IsBeingCarriedByPlatform())
        {
            player.gravityScale = 0f;
            player.linearVelocity = platformCarryVelocity;
            return;
        }

        if (!IsPlayerControlled())
        {
            player.gravityScale = normalGravityScale;
            player.linearVelocity = new Vector2(0f, player.linearVelocity.y);
            return;
        }

        if (isJumping && !IsGrounded())
            isJumping = false;

        bool isMoving = Mathf.Abs(moveDirection.x) > 0.1f;

        if (isOnSlope && IsGrounded() && !isMoving && !isJumping)
        {
            player.gravityScale = 0f;
            player.linearVelocity = Vector2.zero;
            return;
        }
        else
        {
            player.gravityScale = normalGravityScale;
        }

        player.linearVelocity = new Vector2(moveDirection.x * speed, player.linearVelocity.y);
    }

    private Vector2 ReadMoveInput()
    {
        float actionHorizontal = Mathf.Clamp(ReadVectorAction(move).x, -1f, 1f);
        return new Vector2(actionHorizontal, 0f);
    }

    private static Vector2 ReadVectorAction(InputActionReference actionReference)
    {
        return actionReference == null ? Vector2.zero : actionReference.action.ReadValue<Vector2>();
    }

    private static void SubscribeAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference != null) actionReference.action.started += callback;
    }

    private static void UnsubscribeAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference != null) actionReference.action.started -= callback;
    }

    private bool IsBeingCarriedByPlatform()
    {
        return platformCarryUntilTime > 0f && Time.fixedTime <= platformCarryUntilTime;
    }

    private void CachePlayerColliders()
    {
        playerColliders = player != null
            ? player.GetComponentsInChildren<Collider2D>(true)
            : GetComponentsInChildren<Collider2D>(true);
    }
}
