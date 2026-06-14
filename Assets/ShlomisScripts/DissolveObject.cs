using System.Collections;
using UnityEngine;

public class DissolveObject : MonoBehaviour
{
    [Header("Dissolve Settings")]
    public float dissolveDuration = 2f;
    public string playerTag = "Player";

    [Header("Rising Object")]
    public GameObject risingObject;

    [Header("Debug")]
    public bool triggerDissolve = false;

    private bool hasDissolveStarted = false;

    private void Update()
    {
        if (triggerDissolve)
        {
            triggerDissolve = false;
            Dissolve();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
            Dissolve();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
            Dissolve();
    }

    public void Dissolve()
    {
        if (hasDissolveStarted) return;
        hasDissolveStarted = true;

        // Activate rising object right away
        if (risingObject != null)
        {
            risingObject.SetActive(true);
            RisingObject riser = risingObject.GetComponent<RisingObject>();
            if (riser != null)
                riser.StartRising();
        }

        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
            startColors[i] = spriteRenderers[i].color;

        float elapsed = 0f;

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dissolveDuration;

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                Color c = startColors[i];
                spriteRenderers[i].color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        // Disable collider only after fully dissolved
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        Destroy(gameObject);
    }
}