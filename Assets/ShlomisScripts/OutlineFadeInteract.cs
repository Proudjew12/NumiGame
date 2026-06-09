using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class OutlineFadeInteract : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference interactAction;

    [Header("Light Reference")]
    [Tooltip("Drag your GlobalLight 2D here")]
    public Light2D targetLight;

    [Header("Fade Settings")]
    public float fadeDuration = 1f;
    public float targetIntensity = 1f; // intensity to fade TO (e.g. 1 = full bright)

    private SpriteOutline spriteOutline;
    private bool isFading = false;

    void Awake()
    {
        spriteOutline = GetComponent<SpriteOutline>();

        if (spriteOutline == null)
            Debug.LogWarning($"[OutlineFadeInteract] No SpriteOutline found on {gameObject.name}!");

        if (targetLight == null)
            Debug.LogWarning($"[OutlineFadeInteract] No Light2D assigned on {gameObject.name}!");
    }

    void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.performed += OnInteract;
    }

    void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.performed -= OnInteract;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (spriteOutline == null || targetLight == null) return;
        if (spriteOutline.currentOutlineSize <= 0f) return;
        if (isFading) return;

        StartCoroutine(FadeLight());
    }

    private IEnumerator FadeLight()
    {
        isFading = true;

        float elapsed = 0f;
        float startIntensity = targetLight.intensity;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            targetLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsed / fadeDuration);
            yield return null;
        }

        targetLight.intensity = targetIntensity;
        isFading = false;

        OnFadeComplete();
    }

    private void OnFadeComplete()
    {
        Debug.Log($"[OutlineFadeInteract] Light faded on {gameObject.name}");
    }
}