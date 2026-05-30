using UnityEngine;
using UnityEngine.InputSystem;

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

     public static NomiMovment instance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        moveDirection = ReadMoveInput();
        HandleFlip();
        AnimationHandler();
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
    

        if (IsGrounded())
        {
            player.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            animator.SetTrigger("Jump");
        }
        
    }

    private bool IsGrounded()
    {
        return groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
        animator.SetBool("Grounded2",IsGrounded());
        animator.SetBool("IsFalling",IsFalling());
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
          player.linearVelocity = new Vector2(moveDirection.x * speed, player.linearVelocity.y);
    }
  private Vector2 ReadMoveInput()
{
    float actionHorizontal = Mathf.Clamp(ReadVectorAction(move).x, -1f, 1f);
    return new Vector2(actionHorizontal, 0f); // ← was `horizontal`, undefined variable
}

    private static Vector2 ReadVectorAction(InputActionReference actionReference)
    {
        return actionReference == null ? Vector2.zero : actionReference.action.ReadValue<Vector2>();
    }

    private static void SubscribeAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference != null)
        {
            actionReference.action.started += callback;
        }
    }

    private static void UnsubscribeAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
    {
        if (actionReference != null)
        {
            actionReference.action.started -= callback;
        }
    }

   
}




