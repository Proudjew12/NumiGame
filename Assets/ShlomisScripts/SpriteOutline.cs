using UnityEngine;

public class SpriteOutline : MonoBehaviour
{
    public Color outlineColor = Color.white;
    public Shader outlineShader;

    private SpriteRenderer spriteRenderer;
    public float currentOutlineSize = 0f;
    private bool _outlineLocked = false; // when true, SetInRange cannot turn outline back on

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline(0f);
    }

    public void LockOutlineOff()
    {
        _outlineLocked = true;
        CreateOutline(0f);
    }

    public void SetInRange(bool inRange)
    {
        if (_outlineLocked) return; // ignore all future calls
        CreateOutline(inRange ? 0.1f : 0f);
    }

    void CreateOutline(float size)
    {
        currentOutlineSize = size;

        foreach (Transform child in transform)
            if (child.name == "Outline")
                Destroy(child.gameObject);

        if (size <= 0f) return;

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
            outline.transform.localPosition = (Vector3)(dir * size);
            outline.transform.localScale = Vector3.one;
            outline.transform.localRotation = Quaternion.identity;

            SpriteRenderer sr = outline.AddComponent<SpriteRenderer>();
            sr.sprite = spriteRenderer.sprite;
            sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sr.sortingOrder = spriteRenderer.sortingOrder - 1;
            sr.flipX = spriteRenderer.flipX;
            sr.flipY = spriteRenderer.flipY;

            if (outlineShader != null)
            {
                Material mat = new Material(outlineShader);
                mat.color = outlineColor;
                sr.material = mat;
                sr.color = Color.white;
            }
            else
            {
                sr.color = outlineColor;
            }
        }
    }

    void LateUpdate()
    {
        foreach (Transform child in transform)
        {
            if (child.name != "Outline") continue;
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = spriteRenderer.flipX;
                sr.flipY = spriteRenderer.flipY;
            }
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline(currentOutlineSize);
    }
}