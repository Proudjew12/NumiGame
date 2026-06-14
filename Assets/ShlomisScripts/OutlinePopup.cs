using UnityEngine;

public class OutlinePopup : MonoBehaviour
{
    [Header("Popup Mode")]
    [Tooltip("Use a Prefab (with lights, colliders, etc.) instead of a plain sprite.")]
    public bool usePrefab = false;

    [Header("Sprite Popup (usePrefab = false)")]
    public Sprite popupSprite;

    [Header("Prefab Popup (usePrefab = true)")]
    [Tooltip("Drag your popup prefab here (can contain lights, sprites, etc.)")]
    public GameObject popupPrefab;

    [Header("Shared Settings")]
    public Vector3 popupOffset = new Vector3(0, 1.5f, 0);
    public float fadeSpeed = 5f;

    [Header("Scale Animation")]
    [Tooltip("Scale the popup up/down when showing/hiding.")]
    public bool useScaleAnimation = true;
    public Vector3 hiddenScale   = Vector3.zero;
    public Vector3 visibleScale  = Vector3.one;
    public float scaleSpeed = 5f;

    [Header("Sorting (Sprite mode only)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private GameObject popupInstance;
    private SpriteRenderer popupRenderer; // only used in sprite mode
    private SpriteOutline selfOutline;
    private SpriteOutline parentOutline;
    private SpriteOutline[] childOutlines;

    void Start()
    {
        selfOutline   = GetComponent<SpriteOutline>();
        parentOutline = transform.parent != null
                        ? transform.parent.GetComponent<SpriteOutline>()
                        : null;
        childOutlines = GetComponentsInChildren<SpriteOutline>();

        SpawnPopup();
    }

    void SpawnPopup()
    {
        if (usePrefab)
        {
            if (popupPrefab == null) return;

            popupInstance = Instantiate(popupPrefab, transform.position + popupOffset, Quaternion.identity);
            popupInstance.SetActive(false);

            if (useScaleAnimation)
                popupInstance.transform.localScale = hiddenScale;
        }
        else
        {
            if (popupSprite == null) return;

            popupInstance = new GameObject("OutlinePopup");
            popupInstance.transform.position   = transform.position + popupOffset;
            popupInstance.transform.localScale = useScaleAnimation ? hiddenScale : visibleScale;

            popupRenderer = popupInstance.AddComponent<SpriteRenderer>();
            popupRenderer.sprite           = popupSprite;
            popupRenderer.sortingLayerName = sortingLayerName;
            popupRenderer.sortingOrder     = sortingOrder;

            Color c = popupRenderer.color;
            c.a = 0f;
            popupRenderer.color = c;
        }
    }

    bool IsAnyOutlineActive()
    {
        if (selfOutline   != null && selfOutline.currentOutlineSize   > 0f) return true;
        if (parentOutline != null && parentOutline.currentOutlineSize > 0f) return true;
        foreach (var outline in childOutlines)
            if (outline != null && outline.currentOutlineSize > 0f) return true;
        return false;
    }

    void Update()
    {
        if (popupInstance == null) return;

        popupInstance.transform.position = transform.position + popupOffset;

        bool outlineActive = IsAnyOutlineActive();

        if (usePrefab)
        {
            if (useScaleAnimation)
            {
                // Keep the object active and drive scale so the animation plays
                popupInstance.SetActive(true);

                Vector3 targetScale = outlineActive ? visibleScale : hiddenScale;
                popupInstance.transform.localScale = Vector3.Lerp(
                    popupInstance.transform.localScale,
                    targetScale,
                    Time.deltaTime * scaleSpeed
                );
            }
            else
            {
                // Original behaviour: simple toggle
                popupInstance.SetActive(outlineActive);
            }
        }
        else
        {
            if (popupRenderer == null) return;

            // Fade alpha
            float targetAlpha = outlineActive ? 1f : 0f;
            Color c = popupRenderer.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
            popupRenderer.color = c;

            // Scale animation
            if (useScaleAnimation)
            {
                Vector3 targetScale = outlineActive ? visibleScale : hiddenScale;
                popupInstance.transform.localScale = Vector3.Lerp(
                    popupInstance.transform.localScale,
                    targetScale,
                    Time.deltaTime * scaleSpeed
                );
            }
        }
    }

    void OnDestroy()
    {
        if (popupInstance != null)
            Destroy(popupInstance);
    }
}