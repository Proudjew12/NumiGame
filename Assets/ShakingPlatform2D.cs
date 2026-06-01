using UnityEngine;

public class ShakingPlatform2D : MonoBehaviour
{
    [Header("Shake Settings")]
    public float timeBeforeFall   = 2f;
    public float shakeMagnitude   = 0.05f;
    public float shakeSpeed       = 50f;

    [Header("Return Settings")]
    [Tooltip("Seconds after falling before the platform rises back")]
    public float resetDelay  = 3f;
    [Tooltip("Units per second the platform travels back to origin")]
    public float returnSpeed = 3f;
    [Tooltip("How fast the rotation smooths back to zero during return")]
    public float rotationReturnSpeed = 90f; // degrees per second

    // ── private state ──────────────────────────────────────────────
    private Rigidbody2D rb;
    private Vector2 originalPosition;
    private float   timer      = 0f;
    private float   resetTimer = 0f;
    public  bool    playerOnTop = false;
    private bool    hasFallen   = false;
    private bool    isReturning = false;

    private bool positionLocked = false;

    void Start()
{
    rb = GetComponent<Rigidbody2D>();
    rb.bodyType = RigidbodyType2D.Kinematic;
    // originalPosition is set on first player contact, not here
}

    void Update()
    {
        // ── Phase 3: animate back up ───────────────────────────────
        if (isReturning)
        {
            // Move position toward origin
            Vector2 next = Vector2.MoveTowards(rb.position, originalPosition, returnSpeed * Time.deltaTime);
            rb.MovePosition(next);

            // Smoothly rotate back to 0
            float currentAngle = rb.rotation;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, 0f, rotationReturnSpeed * Time.deltaTime);
            rb.SetRotation(newAngle);

            // Finish when both position and rotation are close enough
            bool positionDone = Vector2.Distance(rb.position, originalPosition) < 0.02f;
            bool rotationDone = Mathf.Abs(Mathf.DeltaAngle(newAngle, 0f)) < 0.5f;

            if (positionDone && rotationDone)
                FinishReturn();

            return;
        }

        // ── Phase 2: sitting at the bottom, counting down ─────────
        if (hasFallen)
        {
            resetTimer += Time.deltaTime;

            if (resetTimer >= resetDelay)
            {
                rb.bodyType        = RigidbodyType2D.Kinematic;
                rb.linearVelocity  = Vector2.zero;
                rb.angularVelocity = 0f;
                isReturning        = true;
            }
            return;
        }

        // ── Phase 0: idle ─────────────────────────────────────────
        if (!playerOnTop) return;

        // ── Phase 1: shaking ──────────────────────────────────────
        timer += Time.deltaTime;

        float progress  = Mathf.Clamp01(timer / timeBeforeFall);
        float intensity = shakeMagnitude * progress;

        transform.position = (Vector3)(originalPosition + new Vector2(
            Mathf.Sin(Time.time * shakeSpeed) * intensity, 0f));

        if (timer >= timeBeforeFall)
            Fall();
    }



void OnCollisionEnter2D(Collision2D collision)
{
    if (!collision.gameObject.CompareTag("Player")) return;
    if (isReturning || hasFallen) return;

    // Lock the position the first time the player ever touches it
    if (!positionLocked)
    {
        originalPosition = rb.position;
        positionLocked   = true;
    }

    playerOnTop = true;
}

    void Fall()
    {
        hasFallen   = true;
        playerOnTop = false;
        resetTimer  = 0f;
        transform.position = originalPosition;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    void FinishReturn()
{
    rb.MovePosition(originalPosition);
    rb.SetRotation(0f);
    transform.position = originalPosition;
    transform.rotation = Quaternion.identity;
    rb.bodyType        = RigidbodyType2D.Kinematic;
    rb.linearVelocity  = Vector2.zero;
    rb.angularVelocity = 0f;

    hasFallen   = false;
    isReturning = false;
    playerOnTop = false; // always clear — don't trust whatever fired during return
    timer       = 0f;
    resetTimer  = 0f;
}
}