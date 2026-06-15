using UnityEngine;

public class RisingObject : MonoBehaviour
{
    [Header("Rise Settings")]
    public float riseSpeed = 3f;

    [Header("Player Detection")]
    public string playerTag = "Player";

    private bool isRising = false;
    private bool hasReachedPlayer = false;

    public RisingFlashVFX flashVFX;

    public void StartRising()
    {
        isRising = true;
    }

    private void Update()
    {
        if (!isRising) return;

        transform.Translate(Vector2.up * riseSpeed * Time.deltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
            OnReachPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            OnReachPlayer();
    }

    private void OnReachPlayer()
    {
        if (hasReachedPlayer) return;
        hasReachedPlayer = true;

        Debug.Log($"{gameObject.name} reached the player!");

        if (flashVFX != null)
            flashVFX.StartRising();

        // Add your logic here: deal damage, trigger animation, etc.
    }
}