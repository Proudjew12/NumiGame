using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightFadeOnTrigger2D : MonoBehaviour
{
    [Header("Light Reference")]
    [Tooltip("Drag your GlobalLight 2D here")]
    public Light2D targetLight;

    [Header("Fade Settings")]
    public float fadeInDuration = 1.5f;
    public float fadeOutDuration = 1.5f;
    public float targetIntensity = 0f; // how dark to go

    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Teleport Settings")]
    public bool teleportOnDark = false;
    public Transform teleportDestination;
    public float holdDuration = 0.2f;
    public bool fadeOutAfterTeleport = true;

    private bool isFading = false;
    private float originalIntensity;
    private GameObject playerObject;

    private void Start()
    {
        if (targetLight != null)
            originalIntensity = targetLight.intensity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag) && !isFading)
        {
            playerObject = other.gameObject;
            StartCoroutine(FadeIn());
        }
    }

    // private void OnTriggerExit2D(Collider2D other)
    // {
    //     if (other.CompareTag(playerTag) && !teleportOnDark)
    //     {
    //         StopAllCoroutines();
    //         StartCoroutine(FadeOut());
    //     }
    // }

    private IEnumerator FadeIn()
    {
        isFading = true;
        float elapsed = 0f;
        float startIntensity = targetLight.intensity;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            targetLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsed / fadeInDuration);
            yield return null;
        }
        targetLight.intensity = targetIntensity;

        if (teleportOnDark && teleportDestination != null && playerObject != null)
        {
            yield return new WaitForSeconds(holdDuration);
            playerObject.transform.position = teleportDestination.position;

            if (fadeOutAfterTeleport)
                yield return StartCoroutine(FadeOut());
        }

        isFading = false;
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float startIntensity = targetLight.intensity;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            targetLight.intensity = Mathf.Lerp(startIntensity, originalIntensity, elapsed / fadeOutDuration);
            yield return null;
        }

        targetLight.intensity = originalIntensity;
        isFading = false;
    }
}