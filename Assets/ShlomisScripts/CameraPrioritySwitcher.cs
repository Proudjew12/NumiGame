using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Attach to a GameObject with a 2D Trigger Collider.
/// When the player enters, the target Cinemachine camera smoothly zooms
/// its Orthographic Size to the chosen target. On exit it zooms back.
/// </summary>
public class CameraZoomSwitcher : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("The Cinemachine camera whose Orthographic Size will be animated.")]
    public CinemachineCamera targetCamera;

    [Header("Zoom Settings")]
    [Tooltip("The Orthographic Size to zoom TO when the player is inside the zone.")]
    public float targetOrthographicSize = 5.9f;

    [Tooltip("The Orthographic Size to return to when the player leaves the zone.")]
    public float defaultOrthographicSize = 3f;

    [Tooltip("How fast the zoom lerps. Higher = snappier.")]
    public float zoomSpeed = 2f;

    [Header("Zone Settings")]
    [Tooltip("Toggle to enable / disable this zone.")]
    public bool zoneEnabled = true;

    [Tooltip("Tag used to identify the player.")]
    public string playerTag = "Player";

    // -------------------------------------------------------
    private Coroutine _zoomCoroutine;
    private CinemachineCamera _lens;          // cached ref

    private void Start()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning($"[CameraZoomSwitcher] No camera assigned on '{gameObject.name}'.");
            return;
        }
        _lens = targetCamera;
        _lens.Lens.OrthographicSize = defaultOrthographicSize;
    }

    // -------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!zoneEnabled) return;
        if (!other.CompareTag(playerTag)) return;
        StartZoom(targetOrthographicSize);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!zoneEnabled) return;
        if (!other.CompareTag(playerTag)) return;
        StartZoom(defaultOrthographicSize);
    }

    // -------------------------------------------------------
    private void StartZoom(float toSize)
    {
        if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
        _zoomCoroutine = StartCoroutine(ZoomRoutine(toSize));
    }

    private IEnumerator ZoomRoutine(float toSize)
    {
        while (!Mathf.Approximately(_lens.Lens.OrthographicSize, toSize))
        {
            float current = _lens.Lens.OrthographicSize;
            float next = Mathf.Lerp(current, toSize, zoomSpeed * Time.deltaTime);

            // Snap when close enough
            if (Mathf.Abs(next - toSize) < 0.001f) next = toSize;

            var lensSettings = _lens.Lens;
            lensSettings.OrthographicSize = next;
            _lens.Lens = lensSettings;

            yield return null;
        }
        _zoomCoroutine = null;
    }

    // -------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = zoneEnabled
            ? new Color(0f, 0.8f, 1f, 0.2f)
            : new Color(1f, 0f, 0f, 0.15f);

        var col2D = GetComponent<Collider2D>();

        if (col2D is BoxCollider2D box)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = zoneEnabled ? new Color(0f, 0.8f, 1f, 0.9f) : new Color(1f, 0f, 0f, 0.7f);
            Gizmos.DrawWireCube(box.offset, box.size);
        }
        else if (col2D is CircleCollider2D circle)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = transform.TransformPoint(circle.offset);
            float radius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = zoneEnabled ? new Color(0f, 0.8f, 1f, 0.9f) : new Color(1f, 0f, 0f, 0.7f);
            Gizmos.DrawWireSphere(center, radius);
        }

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            zoneEnabled
                ? $"ZOOM ZONE  →  size {targetOrthographicSize}\n{(targetCamera != null ? targetCamera.name : "no camera")}"
                : "ZONE DISABLED"
        );
    }
#endif
}