using UnityEngine;

public class RisingObject : MonoBehaviour
{
    [Header("Rise Settings")]
    public float riseSpeed = 3f;

    [Header("Player Detection")]
    public string playerTag = "Player";

    private bool isRising = false;
    private bool hasReachedPlayer = false;

    public void StartRising()
    {
        isRising = true;
    }

    private void Update()
    {
        if (!isRising || hasReachedPlayer) return;

        transform.Translate(Vector2.up * riseSpeed * Time.deltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
            StopRising(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
            StopRising(other.gameObject);
    }

    private void StopRising(GameObject player)
    {
        if (hasReachedPlayer) return;
        hasReachedPlayer = true;
        isRising = false;

        Debug.Log($"{gameObject.name} reached the player!");

        // Add your logic here: deal damage, trigger animation, etc.
    }
}