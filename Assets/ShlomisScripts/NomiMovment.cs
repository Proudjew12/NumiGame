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

    // Platform carry
    private Transform _carryParent;
    // Offset stored in the platform's LOCAL space so it automatically
    // scales and moves with the platform every frame — no drift when
    // the stone grows or shrinks.
    private Vector2 _carryOffsetLocal;
    private float   _carryUntilTime;

    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private NomiFootstepAudio footstepAudio;

    private Transform lastFollowTarget;

    public static NomiMovment instance;

    public Transform  GroundCheck       => groundCheck;
    public float      GroundCheckRadius => groundCheckRadius;
    public LayerMask  GroundLayer       => groundLayer;

    public Collider2D[] SolidColliders
    {
        get
        {
            if (playerColliders == null || playerColliders.Length == 0)
                CachePlayerColliders();
            return playerColliders;
        }
    }

    public bool       IsGroundedForAudio         => IsGrounded();
    public bool       IsPlayerControlledForAudio => IsPlayerControlled();
    public float      HorizontalInputForAudio    => IsPlayerControlled() ? moveDirection.x : 0f;
    public float      HorizontalSpeedForAudio    => player != null ? Mathf.Abs(player.linearVelocity.x) : 0f;
    public Collider2D GroundColliderForAudio     => GetGroundCollider();
    public Collider2D LastGroundColliderForAudio => lastGroundColliderForAudio;

    public bool HasGroundColliderForAudio(Collider2D target) => IsStandingOn(target);

    private void Awake() => CachePlayerColliders();

    public bool IsStandingOn(Collider2D target)
    {
        if (target == null || groundCheck == null) return false;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            groundCheck.position, groundCheckRadius, groundHitsForAudio, groundLayer);

        for (int i = 0; i < hitCount; i++)
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

    // Called every FixedUpdate by InteractableManipulator while the player
    // is standing on the platform.
    public void AttachToPlatform(Transform platformTransform)
    {
        if (player == null || platformTransform == null) return;

        bool alreadyAttached = _carryParent == platformTransform;

        _carryParent    = platformTransform;
        _carryUntilTime = Time.time + 0.15f;

        if (!alreadyAttached)
        {
            // Convert the player's world position into the platform's local space.
            // When the stone scales up/down, InverseTransformPoint accounts for
            // that scale, so the stored offset stays proportionally correct.
            _carryOffsetLocal = platformTransform.InverseTransformPoint(player.position);

            player.bodyType       = RigidbodyType2D.Kinematic;
            player.linearVelocity = Vector2.zero;
            player.gravityScale   = 0f;
        }
    }

    public void ClearPlatformCarry()
    {
        if (_carryParent == null) return;

        _carryParent    = null;
        _carryUntilTime = 0f;

        if (player != null)
        {
            player.bodyType       = RigidbodyType2D.Dynamic;
            player.gravityScale   = normalGravityScale;
            player.linearVelocity = Vector2.zero;
        }
    }

    // Kept for backwards compatibility — no longer does anything.
    public void MoveWithPlatform(Vector2 targetPosition, float deltaTime) { }

    // Called by InteractableManipulator to flip the player to face the
    // direction the stone is moving, even when the camera is on the stone
    // and the player is not directly controlled.
    public void SetFacingFromPlatformInput(float horizontalInput)
    {
        if (horizontalInput > 0.01f)
            transform.localScale = new Vector3( 0.85f, 0.85f, 0.85f);
        else if (horizontalInput < -0.01f)
            transform.localScale = new Vector3(-0.85f, 0.85f, 0.85f);
    }

    void Update()
    {
        CheckCameraTarget();

        if (!IsPlayerControlled()) return;

        moveDirection = ReadMoveInput();
        HandleFlip();

        if (IsBeingCarriedByPlatform())
        {
            animator.SetBool("Grounded2", true);
            animator.SetBool("IsFalling", false);
            animator.SetFloat("Speed", 0f);
            return;
        }

        AnimationHandler();
        CheckSlope();
    }

    void FixedUpdate()
    {
        if (_carryParent != null)
        {
            if (Time.time <= _carryUntilTime)
            {
                // Convert the stored local-space offset back to world space.
                // TransformPoint applies the platform's current position, rotation,
                // AND scale — so if the stone grew, the player sits proportionally
                // higher/further, perfectly tracking the surface.
                Vector2 targetPos = _carryParent.TransformPoint(_carryOffsetLocal);
                player.position       = targetPos;
                player.linearVelocity = Vector2.zero;
                return;
            }
            else
            {
                ClearPlatformCarry();
            }
        }

        if (!IsPlayerControlled())
        {
            player.gravityScale   = normalGravityScale;
            player.linearVelocity = new Vector2(0f, player.linearVelocity.y);
            return;
        }

        if (isJumping && !IsGrounded())
            isJumping = false;

        bool isMoving = Mathf.Abs(moveDirection.x) > 0.1f;

        if (isOnSlope && IsGrounded() && !isMoving && !isJumping)
        {
            player.gravityScale   = 0f;
            player.linearVelocity = Vector2.zero;
            return;
        }

        player.gravityScale   = normalGravityScale;
        player.linearVelocity = new Vector2(moveDirection.x * speed, player.linearVelocity.y);
    }

    private void CheckSlope()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);

        isOnSlope = hit && Vector2.Angle(hit.normal, Vector2.up) > minSlopeAngle;
    }

    private void CheckCameraTarget()
    {
        if (playerCamera == null) return;

        Transform currentTarget = playerCamera.Follow;
        if (currentTarget == lastFollowTarget) return;

        lastFollowTarget = currentTarget;

        var composer = playerCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null) return;

        composer.TargetOffset = currentTarget == this.transform
            ? new Vector3(composer.TargetOffset.x, 3f, composer.TargetOffset.z)
            : new Vector3(composer.TargetOffset.x, 0f, composer.TargetOffset.z);
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
        if (!IsGrounded()) return;

        ClearPlatformCarry();

        player.gravityScale   = normalGravityScale;
        player.bodyType       = RigidbodyType2D.Dynamic;
        isJumping             = true;

        player.linearVelocity = new Vector2(player.linearVelocity.x, 0f);
        player.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        animator.SetTrigger("Jump");
        FindAudioReferences();
        footstepAudio?.PlayJumpFromInput();
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
        var col = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : null;

        if (col != null) lastGroundColliderForAudio = col;
        return col;
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
            transform.localScale = new Vector3( 0.85f, 0.85f, 0.85f);
        else if (moveDirection.x < 0f)
            transform.localScale = new Vector3(-0.85f, 0.85f, 0.85f);
    }

    private Vector2 ReadMoveInput()
    {
        float h = Mathf.Clamp(ReadVectorAction(move).x, -1f, 1f);
        return new Vector2(h, 0f);
    }

    private static Vector2 ReadVectorAction(InputActionReference r) =>
        r == null ? Vector2.zero : r.action.ReadValue<Vector2>();

    private static void SubscribeAction(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
    { if (r != null) r.action.started += cb; }

    private static void UnsubscribeAction(InputActionReference r, System.Action<InputAction.CallbackContext> cb)
    { if (r != null) r.action.started -= cb; }

    private bool IsBeingCarriedByPlatform() =>
        _carryParent != null && Time.time <= _carryUntilTime;

    private void CachePlayerColliders()
    {
        playerColliders = player != null
            ? player.GetComponentsInChildren<Collider2D>(true)
            : GetComponentsInChildren<Collider2D>(true);
    }
}