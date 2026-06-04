using UnityEngine;

/// <summary>
/// WaterOrbController
/// ──────────────────
/// Attach to any GameObject that has a MeshRenderer (e.g. a Quad or Plane).
/// Assign the WaterOrb material in the Inspector, then tweak every setting live.
///
/// To control fill from code:
///     GetComponent<WaterOrbController>().fillAmount = 0.75f;
/// </summary>
[ExecuteAlways]
public class WaterOrbController : MonoBehaviour
{
    // ── Material Reference ────────────────────────────────────────────────
    [Header("Material")]
    [Tooltip("Assign the WaterOrb material here. If left empty the component will " +
             "try to use the MeshRenderer's sharedMaterial.")]
    public Material orbMaterial;

    // ══════════════════════════════════════════════════════════════════════
    //  FILL & WAVE
    // ══════════════════════════════════════════════════════════════════════
    [Header("Fill & Wave")]
    [Range(0f, 1f)]
    [Tooltip("How full the orb is (0 = empty, 1 = full).")]
    public float fillAmount = 0.40f;

    [Range(0f, 0.15f)]
    [Tooltip("Height of the wave crests.")]
    public float waveAmplitude = 0.04f;

    [Range(0f, 20f)]
    [Tooltip("Horizontal frequency of wave 1.")]
    public float waveFrequency = 8f;

    [Range(0f, 5f)]
    [Tooltip("Speed of wave 1.")]
    public float waveSpeed = 1.2f;

    [Range(0f, 30f)]
    [Tooltip("Horizontal frequency of secondary wave.")]
    public float waveFrequency2 = 14f;

    [Range(0f, 5f)]
    [Tooltip("Speed of secondary wave.")]
    public float waveSpeed2 = 0.85f;

    // ══════════════════════════════════════════════════════════════════════
    //  WATER COLORS
    // ══════════════════════════════════════════════════════════════════════
    [Header("Water Colors")]
    [Tooltip("Color at the very surface of the water.")]
    public Color waterColorTop = new Color(0.62f, 0.57f, 0.94f, 0.75f);

    [Tooltip("Color in the mid-section of the water.")]
    public Color waterColorMid = new Color(0.47f, 0.39f, 0.86f, 0.85f);

    [Tooltip("Color at the bottom of the water.")]
    public Color waterColorBot = new Color(0.27f, 0.22f, 0.67f, 0.95f);

    [Tooltip("Foam / highlight line on the wave surface.")]
    public Color foamColor = new Color(0.87f, 0.84f, 1f, 0.5f);

    [Range(0f, 0.05f)]
    [Tooltip("Thickness of the foam line.")]
    public float foamThickness = 0.012f;

    // ══════════════════════════════════════════════════════════════════════
    //  INTERIOR BACKGROUND
    // ══════════════════════════════════════════════════════════════════════
    [Header("Interior Background")]
    public Color bgColorInner = new Color(0.10f, 0.09f, 0.25f, 1f);
    public Color bgColorOuter = new Color(0.04f, 0.04f, 0.13f, 1f);

    // ══════════════════════════════════════════════════════════════════════
    //  BLOB SHAPE
    // ══════════════════════════════════════════════════════════════════════
    [Header("Blob Shape")]
    [Range(0.1f, 0.6f)]
    [Tooltip("Base radius of the orb (in UV space).")]
    public float blobRadius = 0.38f;

    [Range(0f, 10f)]
    public float blobNoiseFreq1 = 3f;

    [Range(0f, 0.1f)]
    public float blobNoiseAmp1 = 0.045f;

    [Range(0f, 15f)]
    public float blobNoiseFreq2 = 5f;

    [Range(0f, 0.1f)]
    public float blobNoiseAmp2 = 0.03f;

    // ══════════════════════════════════════════════════════════════════════
    //  GLOW & RIM
    // ══════════════════════════════════════════════════════════════════════
    [Header("Glow & Rim")]
    public Color rimColor       = new Color(0.74f, 0.71f, 1f, 1f);

    [Range(0f, 0.04f)]
    public float rimWidth       = 0.012f;

    public Color glowColor      = new Color(0.47f, 0.43f, 0.90f, 1f);

    [Range(0f, 0.5f)]
    [Tooltip("How far the outer glow extends beyond the blob edge.")]
    public float glowRadius     = 0.18f;

    [Range(0f, 3f)]
    public float glowIntensity  = 1.2f;

    // ══════════════════════════════════════════════════════════════════════
    //  STARS
    // ══════════════════════════════════════════════════════════════════════
    [Header("Stars")]
    [Range(0, 30)]
    public int   starCount         = 14;

    [Range(0f, 1f)]
    public float starBrightness    = 0.75f;

    [Range(0f, 5f)]
    public float starTwinkleSpeed  = 1.1f;

    // ══════════════════════════════════════════════════════════════════════
    //  INNER GLINT
    // ══════════════════════════════════════════════════════════════════════
    [Header("Inner Glint")]
    [Range(0f, 1f)]
    public float glintIntensity = 0.18f;

    // ── Private ───────────────────────────────────────────────────────────
    private MeshRenderer _renderer;
    private Material     _matInstance; // runtime instance (so we don't dirty the asset)

    // ── Cached property IDs (faster than string lookup every frame) ───────
    static readonly int ID_Fill         = Shader.PropertyToID("_FillAmount");
    static readonly int ID_WaveAmp      = Shader.PropertyToID("_WaveAmplitude");
    static readonly int ID_WaveFreq     = Shader.PropertyToID("_WaveFrequency");
    static readonly int ID_WaveSpd      = Shader.PropertyToID("_WaveSpeed");
    static readonly int ID_WaveFreq2    = Shader.PropertyToID("_WaveFrequency2");
    static readonly int ID_WaveSpd2     = Shader.PropertyToID("_WaveSpeed2");

    static readonly int ID_WaterTop     = Shader.PropertyToID("_WaterColorTop");
    static readonly int ID_WaterMid     = Shader.PropertyToID("_WaterColorMid");
    static readonly int ID_WaterBot     = Shader.PropertyToID("_WaterColorBot");
    static readonly int ID_Foam         = Shader.PropertyToID("_FoamColor");
    static readonly int ID_FoamThick    = Shader.PropertyToID("_FoamThickness");

    static readonly int ID_BgInner      = Shader.PropertyToID("_BgColorInner");
    static readonly int ID_BgOuter      = Shader.PropertyToID("_BgColorOuter");

    static readonly int ID_BlobR        = Shader.PropertyToID("_BlobRadius");
    static readonly int ID_BlobF1       = Shader.PropertyToID("_BlobNoise1");
    static readonly int ID_BlobA1       = Shader.PropertyToID("_BlobNoise1Amp");
    static readonly int ID_BlobF2       = Shader.PropertyToID("_BlobNoise2");
    static readonly int ID_BlobA2       = Shader.PropertyToID("_BlobNoise2Amp");

    static readonly int ID_RimCol       = Shader.PropertyToID("_RimColor");
    static readonly int ID_RimW         = Shader.PropertyToID("_RimWidth");
    static readonly int ID_GlowCol      = Shader.PropertyToID("_GlowColor");
    static readonly int ID_GlowR        = Shader.PropertyToID("_GlowRadius");
    static readonly int ID_GlowI        = Shader.PropertyToID("_GlowIntensity");

    static readonly int ID_StarN        = Shader.PropertyToID("_StarCount");
    static readonly int ID_StarB        = Shader.PropertyToID("_StarBrightness");
    static readonly int ID_StarT        = Shader.PropertyToID("_StarTwinkleSpeed");
    static readonly int ID_Glint        = Shader.PropertyToID("_GlintIntensity");

    // ── Unity lifecycle ───────────────────────────────────────────────────
    void OnEnable()
    {
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null)
        {
            Debug.LogWarning("[WaterOrbController] No MeshRenderer found on this GameObject.");
            return;
        }

        // Use inspector-assigned material or fall back to renderer's material
        Material src = orbMaterial != null ? orbMaterial : _renderer.sharedMaterial;

        if (src == null)
        {
            Debug.LogWarning("[WaterOrbController] No material assigned. Please assign the WaterOrb material.");
            return;
        }

        // Create a runtime instance so we don't dirty the project asset
        _matInstance            = new Material(src);
        _matInstance.name       = src.name + " (Instance)";
        _renderer.material      = _matInstance;

        PushAllProperties();
    }

    void OnDisable()
    {
        // Restore shared material to avoid leaks in the editor
        if (_renderer != null && orbMaterial != null)
            _renderer.sharedMaterial = orbMaterial;

        if (_matInstance != null)
        {
            if (Application.isPlaying)
                Destroy(_matInstance);
            else
                DestroyImmediate(_matInstance);
            _matInstance = null;
        }
    }

    void Update()
    {
        // Push every frame so Inspector tweaks show in real time
        PushAllProperties();
    }

    // ── Push all C# values → shader ──────────────────────────────────────
    void PushAllProperties()
    {
        if (_matInstance == null) return;

        _matInstance.SetFloat (ID_Fill,      fillAmount);
        _matInstance.SetFloat (ID_WaveAmp,   waveAmplitude);
        _matInstance.SetFloat (ID_WaveFreq,  waveFrequency);
        _matInstance.SetFloat (ID_WaveSpd,   waveSpeed);
        _matInstance.SetFloat (ID_WaveFreq2, waveFrequency2);
        _matInstance.SetFloat (ID_WaveSpd2,  waveSpeed2);

        _matInstance.SetColor (ID_WaterTop,  waterColorTop);
        _matInstance.SetColor (ID_WaterMid,  waterColorMid);
        _matInstance.SetColor (ID_WaterBot,  waterColorBot);
        _matInstance.SetColor (ID_Foam,      foamColor);
        _matInstance.SetFloat (ID_FoamThick, foamThickness);

        _matInstance.SetColor (ID_BgInner,   bgColorInner);
        _matInstance.SetColor (ID_BgOuter,   bgColorOuter);

        _matInstance.SetFloat (ID_BlobR,     blobRadius);
        _matInstance.SetFloat (ID_BlobF1,    blobNoiseFreq1);
        _matInstance.SetFloat (ID_BlobA1,    blobNoiseAmp1);
        _matInstance.SetFloat (ID_BlobF2,    blobNoiseFreq2);
        _matInstance.SetFloat (ID_BlobA2,    blobNoiseAmp2);

        _matInstance.SetColor (ID_RimCol,    rimColor);
        _matInstance.SetFloat (ID_RimW,      rimWidth);
        _matInstance.SetColor (ID_GlowCol,   glowColor);
        _matInstance.SetFloat (ID_GlowR,     glowRadius);
        _matInstance.SetFloat (ID_GlowI,     glowIntensity);

        _matInstance.SetFloat (ID_StarN,     starCount);
        _matInstance.SetFloat (ID_StarB,     starBrightness);
        _matInstance.SetFloat (ID_StarT,     starTwinkleSpeed);
        _matInstance.SetFloat (ID_Glint,     glintIntensity);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PUBLIC API  — call these from any other script
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Set fill level (0 = empty, 1 = full). Clamped automatically.</summary>
    public void SetFill(float value)
    {
        fillAmount = Mathf.Clamp01(value);
    }

    /// <summary>Animate fill to a target value over a given duration (coroutine).</summary>
    public System.Collections.IEnumerator AnimateFillTo(float target, float duration)
    {
        float start = fillAmount;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFill(Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }
        SetFill(target);
    }

    /// <summary>Apply a full color preset at once.</summary>
    public void ApplyPreset(WaterOrbPreset preset)
    {
        waterColorTop  = preset.waterColorTop;
        waterColorMid  = preset.waterColorMid;
        waterColorBot  = preset.waterColorBot;
        foamColor      = preset.foamColor;
        rimColor       = preset.rimColor;
        glowColor      = preset.glowColor;
        bgColorInner   = preset.bgColorInner;
        bgColorOuter   = preset.bgColorOuter;
    }
}
