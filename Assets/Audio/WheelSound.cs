using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AudioSource))]
public class WheelSound : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float minSpeedToPlay = 1f;
    [SerializeField] private float fadeStartSpeed = 2f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Audio")]
    [SerializeField] private float maxVolume = 1f;
    [SerializeField] private float fadeSpeed = 6f;

    private Rigidbody2D rb;
    private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
    }

    private void FixedUpdate()
    {
        float speed = Mathf.Abs(rb.linearVelocity.x);

        bool isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        float targetVolume = 0f;

        if (isGrounded && speed > minSpeedToPlay)
        {
            float t = Mathf.InverseLerp(minSpeedToPlay, fadeStartSpeed, speed);
            targetVolume = Mathf.Lerp(0f, maxVolume, t);

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        audioSource.volume = Mathf.MoveTowards(
            audioSource.volume,
            targetVolume,
            fadeSpeed * Time.fixedDeltaTime
        );

        if (audioSource.isPlaying && audioSource.volume <= 0.01f && targetVolume == 0f)
        {
            audioSource.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}