using UnityEngine;

/// <summary>
/// Parallax only runs when the assigned PlayerCamera is the active Camera.main.
/// During any other camera (cutscene, zoom-out, etc.) the layer freezes in place.
///
/// RECOMMENDED FACTOR VALUES:
/// ─────────────────────────────────────────────────────────
///  Sky / solid backdrop         →  0.05   loop: true
///  Far haze / atmosphere        →  0.10   loop: true
///  Fog / cloud layers           →  0.15   loop: true
///  Distant background props     →  0.25   loop: false
///  Mid-ground islands / trees   →  0.40   loop: false
///  Near foreground elements     →  0.60   loop: false
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax")]
    [Range(0f, 1f)]
    [Tooltip("0 = glued to camera  |  1 = fixed in world\nCloser layers = higher value.")]
    public float parallaxFactor = 0.2f;

    [Header("Looping")]
    [Tooltip("Enable for tiling sprites (sky, fog). Disable for unique props.")]
    public bool infiniteLoop = true;

    [Tooltip("Extra tile padding each side. Raise to 2 if seams show when zoomed out.")]
    public int extraTiles = 1;

    [Header("Camera")]
    [Tooltip("Drag your PlayerCamera here. Parallax only runs when this is Camera.main.")]
    public Camera playerCamera;

    // ── Private ────────────────────────────────────────────────────

    private Transform _t;
    private float     _originX;
    private float     _spriteWidth;
    private float     _lastCamX;

    // ── Unity ──────────────────────────────────────────────────────

    void Awake()
    {
        _t       = transform;
        _originX = _t.position.x;

        if (infiniteLoop)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) _spriteWidth = sr.bounds.size.x;
            else            infiniteLoop = false;
        }

        if (playerCamera != null)
            _lastCamX = playerCamera.transform.position.x;
    }

    void LateUpdate()
    {
        // Do nothing if the player camera is not the active camera
        if (playerCamera == null || Camera.main != playerCamera)
            return;

        float camX = playerCamera.transform.position.x;
        float delta = camX - _lastCamX;
        _lastCamX = camX;

        // Move the layer by the parallax portion of the camera's movement
        _originX += delta * parallaxFactor;
        _t.position = new Vector3(_originX, _t.position.y, _t.position.z);

        // Seamless looping
        if (infiniteLoop && _spriteWidth > 0f)
        {
            float boundary = _spriteWidth * (1 + extraTiles);
            float drift    = camX * (1f - parallaxFactor);

            if (drift > _originX + boundary)
                _originX += boundary;
            else if (drift < _originX - boundary)
                _originX -= boundary;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!infiniteLoop) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        float w = sr.bounds.size.x;
        float h = sr.bounds.size.y;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        for (int i = -extraTiles; i <= extraTiles + 1; i++)
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x + w * i, transform.position.y, 0),
                new Vector3(w, h, 0));
    }
#endif
}