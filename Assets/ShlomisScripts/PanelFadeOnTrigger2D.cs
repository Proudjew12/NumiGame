using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this script to the Trigger GameObject (with a Collider2D set to "Is Trigger").
/// When the Player enters the trigger, the Panel's alpha gradually fades to black.
/// Optionally teleports the player to a target location once the screen is fully dark,
/// then fades back out.
/// </summary>
public class PanelFadeOnTrigger2D : MonoBehaviour
{
    [Header("Panel Reference")]
    [Tooltip("Drag your Panel's Image component here")]
    public Image panelImage;

    [Header("Fade Settings")]
    [Tooltip("How long (in seconds) the fade-in takes")]
    public float fadeInDuration = 1.5f;

    [Tooltip("How long (in seconds) the fade-out takes after teleport")]
    public float fadeOutDuration = 1.5f;

    [Tooltip("Target alpha value (0–255 to match Unity's RGB 0-255 display)")]
    [Range(0, 255)]
    public float targetAlpha = 255f;

    [Header("Player Detection")]
    [Tooltip("Tag used to identify the Player object")]
    public string playerTag = "Player";

    [Header("Teleport Settings")]
    [Tooltip("Enable to teleport the player once the screen is fully dark")]
    public bool teleportOnBlack = false;

    [Tooltip("The Transform position the player will be moved to")]
    public Transform teleportDestination;

    [Tooltip("How long to wait at full black before teleporting (seconds)")]
    public float holdDuration = 0.2f;

    [Tooltip("Automatically fade back out after teleporting")]
    public bool fadeOutAfterTeleport = true;

    private bool isFading = false;
    private GameObject playerObject;

    private void Start()
    {
        if (panelImage != null)
        {
            Color c = panelImage.color;
            c.a = 0f;
            panelImage.color = c;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag) && !isFading)
        {
            playerObject = other.gameObject;
            StartCoroutine(FadeInPanel());
        }
    }

    private IEnumerator FadeInPanel()
    {
        isFading = true;

        // --- Fade In ---
        float elapsed = 0f;
        float startAlpha = panelImage.color.a;
        float endAlpha = targetAlpha / 255f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeInDuration);
            SetPanelAlpha(newAlpha);
            yield return null;
        }

        SetPanelAlpha(endAlpha);

        // --- Teleport ---
        if (teleportOnBlack && teleportDestination != null && playerObject != null)
        {
            // Hold at full black briefly
            yield return new WaitForSeconds(holdDuration);

            // Move the player
            playerObject.transform.position = teleportDestination.position;

            // --- Fade Out ---
            if (fadeOutAfterTeleport)
            {
                yield return StartCoroutine(FadeOutPanel());
            }
        }

        isFading = false;
    }

    // Optional: fade out if player exits without teleport enabled
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag) && !teleportOnBlack)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOutPanel());
        }
    }

    private IEnumerator FadeOutPanel()
    {
        float elapsed = 0f;
        float startAlpha = panelImage.color.a;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            SetPanelAlpha(newAlpha);
            yield return null;
        }

        SetPanelAlpha(0f);
        isFading = false;
    }

    private void SetPanelAlpha(float alpha)
    {
        Color c = panelImage.color;
        c.a = alpha;
        panelImage.color = c;
    }
}