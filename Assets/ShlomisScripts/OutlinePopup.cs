using UnityEngine;

public class OutlinePopup : MonoBehaviour
{
    [Header("Popup Settings")]
    public Sprite popupSprite;
    public Vector3 popupOffset = new Vector3(0, 1.5f, 0);
    public float fadeSpeed = 5f;

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private GameObject popupInstance;
    private SpriteRenderer popupRenderer;
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
        if (popupSprite == null) return;

        popupInstance = new GameObject("OutlinePopup");
        popupInstance.transform.position = transform.position + popupOffset;

        popupRenderer = popupInstance.AddComponent<SpriteRenderer>();
        popupRenderer.sprite = popupSprite;
        popupRenderer.sortingLayerName = sortingLayerName;
        popupRenderer.sortingOrder = sortingOrder;

        Color c = popupRenderer.color;
        c.a = 0f;
        popupRenderer.color = c;
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
        if (popupRenderer == null) return;

        popupInstance.transform.position = transform.position + popupOffset;

        float targetAlpha = IsAnyOutlineActive() ? 1f : 0f;

        Color c = popupRenderer.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * fadeSpeed);
        popupRenderer.color = c;
    }

    void OnDestroy()
    {
        if (popupInstance != null)
            Destroy(popupInstance);
    }
}