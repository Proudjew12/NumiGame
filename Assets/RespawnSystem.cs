using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D Respawn System — attach to the Player GameObject.
/// 
/// Requirements on the Player:
///   - Rigidbody2D
///   - CapsuleCollider2D
/// 
/// Setup:
///   1. Add respawn point Transforms to the "Respawn Points" list in the Inspector.
///   2. Tag your death-zone trigger collider(s) as "Respawn" and tick "Is Trigger".
/// 
/// Respawn logic:
///   - Priority: teleports to the CLOSEST point to the LEFT of the player (X < playerX).
///   - Fallback:  if no point exists to the left, uses the globally closest point instead.
/// </summary>
public class RespawnSystem : MonoBehaviour
{
    [Header("Respawn Points")]
    [Tooltip("Drag your respawn point Transforms here in the Inspector.")]
    public List<Transform> respawnPoints = new List<Transform>();

    [Header("Flicker Settings")]
    [Tooltip("Total duration of the flicker effect in seconds.")]
    public float flickerDuration = 1f;

    [Tooltip("How many times per second the sprite toggles on/off.")]
    public float flickerFrequency = 15f;

    // ── Internal references ───────────────────────────────────────────────────
    private Rigidbody2D       rb;
    private CapsuleCollider2D capsule;
    private SpriteRenderer[]  spriteRenderers;

    private bool isRespawning = false;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        rb              = GetComponent<Rigidbody2D>();
        capsule         = GetComponent<CapsuleCollider2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (rb      == null) Debug.LogError("[RespawnSystem] Rigidbody2D not found on Player!");
        if (capsule == null) Debug.LogError("[RespawnSystem] CapsuleCollider2D not found on Player!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2D trigger detection
    // ─────────────────────────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Respawn") && !isRespawning)
        {
            Transform target = GetBestRespawnPoint();
            if (target != null)
                StartCoroutine(DoRespawn(target.position));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Returns the closest respawn point to the LEFT of the player (X < playerX).
    //  Falls back to the globally closest point if none exist to the left.
    // ─────────────────────────────────────────────────────────────────────────
    private Transform GetBestRespawnPoint()
    {
        if (respawnPoints == null || respawnPoints.Count == 0)
        {
            Debug.LogWarning("[RespawnSystem] No respawn points assigned!");
            return null;
        }

        float     playerX    = transform.position.x;

        Transform bestLeft   = null;   // closest point strictly to the left
        float     minLeft    = Mathf.Infinity;

        Transform bestAny    = null;   // globally closest (fallback)
        float     minAny     = Mathf.Infinity;

        foreach (Transform point in respawnPoints)
        {
            if (point == null) continue;

            float dist = Vector2.Distance(transform.position, point.position);

            // Always track the globally closest as a fallback
            if (dist < minAny)
            {
                minAny  = dist;
                bestAny = point;
            }

            // Left side = smaller X than the player
            if (point.position.x < playerX && dist < minLeft)
            {
                minLeft  = dist;
                bestLeft = point;
            }
        }

        if (bestLeft != null)
        {
            Debug.Log("[RespawnSystem] Respawning at closest LEFT point: " + bestLeft.name);
            return bestLeft;
        }

        Debug.Log("[RespawnSystem] No point to the left — falling back to globally closest: " + bestAny.name);
        return bestAny;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Respawn coroutine
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator DoRespawn(Vector2 targetPosition)
    {
        isRespawning = true;

        // ── 1. Kill all current velocity so the player doesn't drift ─────────
        rb.linearVelocity  = Vector2.zero;
        rb.angularVelocity = 0f;

        // ── 2. Teleport via Rigidbody2D (safe with physics) ──────────────────
        rb.position = targetPosition;

        // Also sync the Transform so the camera / other systems see it instantly
        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);

        // ── 3. Flicker for flickerDuration seconds ───────────────────────────
        yield return StartCoroutine(FlickerEffect());

        isRespawning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Flicker coroutine — toggles SpriteRenderers on/off
    // ─────────────────────────────────────────────────────────────────────────
    private IEnumerator FlickerEffect()
    {
        float elapsed        = 0f;
        float toggleInterval = 1f / flickerFrequency;
        float nextToggle     = 0f;
        bool  visible        = true;

        while (elapsed < flickerDuration)
        {
            if (elapsed >= nextToggle)
            {
                visible    = !visible;
                SetVisible(visible);
                nextToggle += toggleInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Guarantee the player is fully visible after the effect ends
        SetVisible(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Toggle all SpriteRenderers on this GameObject and children
    // ─────────────────────────────────────────────────────────────────────────
    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
                sr.enabled = visible;
        }
    }
}