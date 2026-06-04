using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WaterOrbDemo
/// ────────────
/// Optional demo driver.  Wire up:
///   - orbController  → your WaterOrbController
///   - fillSlider     → a UI Slider (0–1) to control fill manually
///   - autoAnimate    → tick to watch the orb fill/drain on its own
/// </summary>
public class WaterOrbDemo : MonoBehaviour
{
    [Header("References")]
    public WaterOrbController orbController;

    [Header("UI (optional)")]
    [Tooltip("A UI Slider (0-1) whose value drives the fill amount.")]
    public Slider fillSlider;

    [Header("Auto Animate")]
    [Tooltip("If true, the orb will automatically fill and drain in a loop.")]
    public bool autoAnimate = false;

    [Range(0.01f, 0.5f)]
    public float animateSpeed = 0.1f;

    private float _animDir = 1f;

    void Start()
    {
        if (fillSlider != null)
        {
            fillSlider.minValue = 0f;
            fillSlider.maxValue = 1f;
            fillSlider.value    = orbController != null ? orbController.fillAmount : 0.4f;
            fillSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    void Update()
    {
        if (!autoAnimate || orbController == null) return;

        float next = orbController.fillAmount + _animDir * animateSpeed * Time.deltaTime;
        if (next >= 1f) { next = 1f; _animDir = -1f; }
        if (next <= 0f) { next = 0f; _animDir =  1f; }
        orbController.SetFill(next);

        if (fillSlider != null)
            fillSlider.SetValueWithoutNotify(orbController.fillAmount);
    }

    void OnSliderChanged(float value)
    {
        if (orbController != null)
            orbController.SetFill(value);
    }

    /// <summary>Call from a UI Button to animate fill to 100% over 2 seconds.</summary>
    public void FillToFull()
    {
        if (orbController != null)
            StartCoroutine(orbController.AnimateFillTo(1f, 2f));
    }

    /// <summary>Call from a UI Button to drain to empty over 2 seconds.</summary>
    public void DrainToEmpty()
    {
        if (orbController != null)
            StartCoroutine(orbController.AnimateFillTo(0f, 2f));
    }
}
