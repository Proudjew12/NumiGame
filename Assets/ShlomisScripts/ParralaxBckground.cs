using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax")]
    [Range(0f, 1f)]
    [Tooltip("0 = glued to camera  |  1 = fixed in world\nCloser layers = higher value.")]
    public float parallaxFactor = 0.2f;

    [Header("Y Axis Parallax")]
    [Tooltip("Enable vertical parallax (useful for up/down camera movement).")]
    public bool enableYParallax = false;

    [Range(0f, 1f)]
    [Tooltip("0 = glued to camera  |  1 = fixed in world\nIndependent Y parallax strength.")]
    public float parallaxFactorY = 0.2f;

    [Header("Looping")]
    [Tooltip("Enable for tiling sprites (sky, fog). Disable for unique props.")]
    public bool infiniteLoop = true;

    [Tooltip("Extra tile padding each side. Raise to 2 if seams show when zoomed out.")]
    public int extraTiles = 1;

    [Header("Auto Scroll")]
    [Tooltip("Enable for objects like the moon to give a slow alive drift across the sky.")]
    public bool autoScroll = false;

    [Tooltip("How fast the object drifts on its own. Try 0.5 to 2.")]
    public float autoScrollSpeed = 1f;

    [Header("Follow Target")]
    [Tooltip("If enabled, parallax follows the Player transform instead of the camera.")]
    public bool followPlayer = false;

    [Tooltip("Drag the Player transform here when followPlayer is enabled.")]
    public Transform playerTransform;

    [Header("Camera")]
    [Tooltip("Drag your PlayerCamera here. Parallax only runs when this is Camera.main.")]
    public Camera playerCamera;

    // ── Private ────────────────────────────────────────────────────

    private Transform _t;
    private float     _originX;
    private float     _originY;
    private float     _spriteWidth;
    private float     _lastTrackedX;
    private float     _lastCamY;

    // ── Unity ──────────────────────────────────────────────────────

    void Awake()
    {
        _t       = transform;
        _originX = _t.position.x;
        _originY = _t.position.y;

        if (infiniteLoop)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) _spriteWidth = sr.bounds.size.x;
            else            infiniteLoop = false;
        }

        // Seed the last position from whichever target we follow
        _lastTrackedX = GetTrackedX();

        if (playerCamera != null)
            _lastCamY = playerCamera.transform.position.y;
    }

    void LateUpdate()
    {
        if (playerCamera == null || Camera.main != playerCamera)
            return;

        float trackedX = GetTrackedX();
        float camY     = playerCamera.transform.position.y;
        float camX     = playerCamera.transform.position.x;

        float delta   = trackedX - _lastTrackedX;
        _lastTrackedX = trackedX;
        _lastCamY     = camY;

        _originX += delta * parallaxFactor;

        if (autoScroll)
            _originX += Time.deltaTime * autoScrollSpeed;

        float newY = enableYParallax
            ? _originY + (camY * (1f - parallaxFactorY))
            : _t.position.y;

        _t.position = new Vector3(_originX, newY, _t.position.z);

        if (infiniteLoop && _spriteWidth > 0f)
        {
            // Looping always uses camera position as the viewport anchor
            float boundary = _spriteWidth * (1 + extraTiles);
            float drift    = camX * (1f - parallaxFactor);

            if (drift > _originX + boundary)
                _originX += boundary;
            else if (drift < _originX - boundary)
                _originX -= boundary;
        }
    }

    private float GetTrackedX()
    {
        if (followPlayer && playerTransform != null)
            return playerTransform.position.x;

        return playerCamera != null ? playerCamera.transform.position.x : 0f;
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