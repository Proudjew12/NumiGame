using UnityEngine;

public class SpriteOutline : MonoBehaviour
{
    public Color outlineColor = Color.red;
    public float outlineSize = 0.05f;

    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline();
    }

    void CreateOutline()
    {
        foreach (Transform child in transform)
        {
            if (child.name == "Outline")
                Destroy(child.gameObject);
        }

        Vector2[] directions = {
            Vector2.up, Vector2.down,
            Vector2.left, Vector2.right,
            new Vector2(1, 1).normalized,
            new Vector2(-1, 1).normalized,
            new Vector2(1, -1).normalized,
            new Vector2(-1, -1).normalized
        };

        foreach (Vector2 dir in directions)
        {
            GameObject outline = new GameObject("Outline");
            outline.transform.parent = transform;
            outline.transform.localPosition = (Vector3)(dir * outlineSize);
            outline.transform.localScale = Vector3.one;
            outline.transform.localRotation = Quaternion.identity; // ✅ stays aligned with parent rotation

            SpriteRenderer sr = outline.AddComponent<SpriteRenderer>();
            sr.sprite = spriteRenderer.sprite;
            sr.color = outlineColor;
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder - 1;
            sr.material = spriteRenderer.material;
            sr.flipX = spriteRenderer.flipX; // ✅ matches parent flip
            sr.flipY = spriteRenderer.flipY; // ✅ matches parent flip
        }
    }

    void LateUpdate()
    {
        // ✅ Sync flip every frame in case it changes at runtime (e.g. character facing direction)
        foreach (Transform child in transform)
        {
            if (child.name == "Outline")
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.flipX = spriteRenderer.flipX;
                    sr.flipY = spriteRenderer.flipY;
                }
            }
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline();
    }
}