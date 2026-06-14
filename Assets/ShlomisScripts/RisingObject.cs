using UnityEngine;

public class RisingObject : MonoBehaviour
{
    [Header("Rise Settings")]
    public float riseSpeed = 3f;

    [Header("Player Detection")]
    public string playerTag = "Player";      // Make sure your player GameObject has this tag

    private bool isRising = false;
    private bool hasReachedPlayer = false;

    // Called by ExplodingObject after explosion
    public void StartRising()
    {
        isRising = true;
    }

    private void Update()
    {
        if (!isRising || hasReachedPlayer) return;

        // Move upward every frame
        transform.Translate(Vector3.up * riseSpeed * Time.deltaTime, Space.World);
    }

    // Stops rising when collider touches the player collider
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            StopRising(collision.gameObject);
        }
    }

    // Also works if the rising object uses a trigger collider
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            StopRising(other.gameObject);
        }
    }

    private void StopRising(GameObject player)
    {
        if (hasReachedPlayer) return;

        hasReachedPlayer = true;
        isRising = false;

        Debug.Log($"{gameObject.name} reached the player: {player.name}");

        // --- Do whatever you need here ---
        // e.g. deal damage, trigger animation, destroy this object, etc.
        // player.GetComponent<PlayerHealth>()?.TakeDamage(10);
    }
}
