using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class StoneInteraction : MonoBehaviour
{
    public GameObject popup;   // a world-space GameObject with a SpriteRenderer

    private bool isPopupOpen = false;
    private SpriteOutline spriteOutline;
    public InputAction interactAction;

    void Awake()
    {
        spriteOutline = GetComponent<SpriteOutline>();
        popup.SetActive(false);
    }

    void OnEnable()
    {
        interactAction.Enable();
        interactAction.performed += OnInteract;
    }

    void OnDisable()
    {
        interactAction.performed -= OnInteract;
        interactAction.Disable();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (spriteOutline == null || spriteOutline.currentOutlineSize <= 0f) return;
        TogglePopup();
    }

    void TogglePopup()
    {
        if (isPopupOpen) ClosePopup();
        else OpenPopup();
    }

    void OpenPopup()
    {
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
        SpriteRenderer sr = popup.GetComponent<SpriteRenderer>();
        popup.transform.localScale = Vector3.one * 0.8f;
        Color c = sr.color;
        c.a = 0f;
        sr.color = c;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.2f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            c.a = s;
            sr.color = c;
            popup.transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, s);
            yield return null;
        }
    }

    IEnumerator AnimateOut()
    {
        SpriteRenderer sr = popup.GetComponent<SpriteRenderer>();
        Color c = sr.color;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.15f;
            float s = Mathf.SmoothStep(0f, 1f, t);
            c.a = 1f - s;
            sr.color = c;
            popup.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, s);
            yield return null;
        }

        popup.SetActive(false);
    }
}