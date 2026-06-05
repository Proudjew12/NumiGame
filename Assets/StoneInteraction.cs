using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class StoneInteraction : MonoBehaviour
{
    public GameObject popup;
    public InputAction interactAction;
    public Transform player;

    private bool isPopupOpen = false;
    private SpriteOutline spriteOutline;
    private CanvasGroup canvasGroup;

    private static List<StoneInteraction> allStones = new();

    void Awake()
    {
        spriteOutline = GetComponent<SpriteOutline>();

        canvasGroup = popup.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popup.AddComponent<CanvasGroup>();

        foreach (var anim in popup.GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        popup.SetActive(false);
    }

    void OnEnable()
    {
        allStones.Add(this);
        interactAction.Enable();
        interactAction.performed += OnInteract;
    }

    void OnDisable()
    {
        allStones.Remove(this);
        interactAction.performed -= OnInteract;
        interactAction.Disable();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        // Only the closest outlined stone handles the interaction
        StoneInteraction closest = GetClosestOutlinedStone();
        if (closest == null || closest != this) return;

        TogglePopup();
    }

    private StoneInteraction GetClosestOutlinedStone()
    {
        // Auto-find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            else return null;
        }

        StoneInteraction nearest = null;
        float minDist = float.MaxValue;

        foreach (var stone in allStones)
        {
            // Skip stones with no active outline
            if (stone.spriteOutline == null || stone.spriteOutline.currentOutlineSize <= 0f)
                continue;

            float dist = Vector2.Distance(player.position, stone.transform.position);

            // Use a small epsilon to break ties deterministically
            if (dist < minDist - 0.01f)
            {
                minDist = dist;
                nearest = stone;
            }
        }

        return nearest;
    }

    void TogglePopup()
    {
        if (isPopupOpen) ClosePopup();
        else OpenPopup();
    }

    void OpenPopup()
    {
        // Close any other open popups first
        foreach (var stone in allStones)
        {
            if (stone != this && stone.isPopupOpen)
                stone.ClosePopup();
        }

        isPopupOpen = true;
        popup.SetActive(true);
        StartCoroutine(AnimateIn());
        Time.timeScale = 0f;
    }

    void ClosePopup()
    {
        isPopupOpen = false;
        StartCoroutine(AnimateOut());
        Time.timeScale = 1f;
    }

    IEnumerator AnimateIn()
    {
        canvasGroup.alpha = 0f;
        popup.transform.localScale = Vector3.one * 0.8f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.2f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = s;
            popup.transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, s);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        popup.transform.localScale = Vector3.one;
    }

    IEnumerator AnimateOut()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.15f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = 1f - s;
            popup.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, s);
            yield return null;
        }

        popup.SetActive(false);
    }
}