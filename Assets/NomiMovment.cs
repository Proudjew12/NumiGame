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

    private Vector2 moveDirection;
    private readonly Collider2D[] groundHitsForAudio = new Collider2D[8];

    [SerializeField] private CinemachineCamera playerCamera;

    // ✅ Track the previous follow target to detect changes
    private Transform lastFollowTarget;

    public static NomiMovment instance;

    public bool IsGroundedForAudio => IsGrounded();
    public bool IsPlayerControlledForAudio => IsPlayerControlled();
    public float HorizontalInputForAudio => IsPlayerControlled() ? moveDirection.x : 0f;
    public float HorizontalSpeedForAudio => player != null ? Mathf.Abs(player.linearVelocity.x) : 0f;
    public Collider2D GroundColliderForAudio => GetGroundCollider();

    public bool HasGroundColliderForAudio(Collider2D target)
    {
        if (target == null || groundCheck == null)
        {
            return false;
        }

        var hitCount = Physics2D.OverlapCircleNonAlloc(
            groundCheck.position,
            groundCheckRadius,
            groundHitsForAudio,
            groundLayer);

        for (var i = 0; i < hitCount; i++)
        {
            if (groundHitsForAudio[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    void Start()
    {
        instance = this;

        if (playerCamera != null)
            lastFollowTarget = playerCamera.Follow;
    }

    void Update()
    {
        CheckCameraTarget();

        if (!IsPlayerControlled()) return; // ✅ Block movement if camera isn't on player

        moveDirection = ReadMoveInput();
        HandleFlip();
        AnimationHandler();
    }

    // ✅ Detects when the camera target changes and updates offset + movement lock
    private void CheckCameraTarget()
    {
        if (playerCamera == null) return;

        Transform currentTarget = playerCamera.Follow;

        if (currentTarget == lastFollowTarget) return; // No change

        lastFollowTarget = currentTarget;

        var composer = playerCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null) return;

        if (currentTarget == this.transform)
        {
            // ✅ Camera returned to player — restore offset and allow movement
            composer.TargetOffset = new Vector3(composer.TargetOffset.x, 3f, composer.TargetOffset.z);
        }
        else
        {
            // ✅ Camera moved away from player — zero Y offset and block movement
            composer.TargetOffset = new Vector3(composer.TargetOffset.x, 0f, composer.TargetOffset.z);
        }
    }

    // ✅ Returns true only when the camera is following the player
    private bool IsPlayerControlled()
    {
        if (playerCamera == null) return true; // Fail open if no camera assigned
        return playerCamera.Follow == this.transform;
    }

    private void OnEnable()
    {
        SubscribeAction(jump, Jump);
    }

    private void OnDisable()
    {
        UnsubscribeAction(jump, Jump);
    }

    private void Jump(InputAction.CallbackContext _)
    {
        if (!IsPlayerControlled()) return; // ✅ Block jump if camera isn't on player

        if (IsGrounded())
        {
             player.linearVelocity = new Vector2(player.linearVelocity.x, 0f);
            player.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            animator.SetTrigger("Jump");
        }
    }

    private bool IsGrounded()
    {
        return groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private Collider2D GetGroundCollider()
    {
        return groundCheck != null ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) : null;
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
    }

    private bool IsFalling()
    {
        return player.linearVelocity.y < -0.1f;
    }

    private void HandleFlip()
    {
        if (moveDirection.x > 0f)
            transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        else if (moveDirection.x < 0f)
            transform.localScale = new Vector3(-0.85f, 0.85f, 0.85f);
    }

    void FixedUpdate()
    {
        if (!IsPlayerControlled())
        {
            // ✅ Stop horizontal movement when camera is not on player
            player.linearVelocity = new Vector2(0f, player.linearVelocity.y);
            return;
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
        if (actionReference != null)
            actionReference.action.started += callback;
    }

    private static void UnsubscribeAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference != null)
            actionReference.action.started -= callback;
    }
}
