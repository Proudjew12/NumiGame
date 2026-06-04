using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class WaterOrbUIController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    //  ILLUSTRATION
    // ══════════════════════════════════════════════════════════════════════
    [Header("Illustration")]
    [Tooltip("Your white-on-black line art texture. Import with Alpha Source = From Gray Scale.")]
    public Texture2D illustrationTexture;

    [Range(0.1f, 5f)]
    [Tooltip("Scale of the illustration inside the orb.")]
    public float illustrationScale = 1.0f;

    [Range(-1f, 1f)]
    public float illustrationOffsetX = 0f;

    [Range(-1f, 1f)]
    public float illustrationOffsetY = 0f;

    [Range(0f, 1f)]
    public float illustrationOpacity = 1.0f;

    [Range(0f, 1f)]
    [Tooltip("0 = illustration stays centred. 1 = illustration rises with the water surface.")]
    public float riseWithWater = 1.0f;

    // ══════════════════════════════════════════════════════════════════════
    //  FILL & WAVE
    // ══════════════════════════════════════════════════════════════════════
    [Header("Fill & Wave")]
    [Range(0f, 1f)]
    public float fillAmount = 0.40f;

    [Range(0f, 0.15f)]
    public float waveAmplitude = 0.04f;

    [Range(0f, 20f)]
    public float waveFrequency = 8f;

    [Range(0f, 5f)]
    public float waveSpeed = 1.2f;

    [Range(0f, 30f)]
    public float waveFrequency2 = 14f;

    [Range(0f, 5f)]
    public float waveSpeed2 = 0.85f;

    // ══════════════════════════════════════════════════════════════════════
    //  WATER COLORS
    // ══════════════════════════════════════════════════════════════════════
    [Header("Water Colors")]
    public Color waterColorTop = new Color(0.62f, 0.57f, 0.94f, 0.75f);
    public Color waterColorMid = new Color(0.47f, 0.39f, 0.86f, 0.85f);
    public Color waterColorBot = new Color(0.27f, 0.22f, 0.67f, 0.95f);
    public Color foamColor     = new Color(0.87f, 0.84f, 1f, 0.50f);

    [Range(0f, 0.05f)]
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
    public float blobRadius     = 0.38f;
    [Range(0f, 10f)]
    public float blobNoiseFreq1 = 3f;
    [Range(0f, 0.1f)]
    public float blobNoiseAmp1  = 0.045f;
    [Range(0f, 15f)]
    public float blobNoiseFreq2 = 5f;
    [Range(0f, 0.1f)]
    public float blobNoiseAmp2  = 0.03f;

    // ══════════════════════════════════════════════════════════════════════
    //  GLOW & RIM
    // ══════════════════════════════════════════════════════════════════════
    [Header("Glow & Rim")]
    public Color rimColor      = new Color(0.74f, 0.71f, 1f, 1f);
    [Range(0f, 0.04f)]
    public float rimWidth      = 0.012f;
    public Color glowColor     = new Color(0.47f, 0.43f, 0.90f, 1f);
    [Range(0f, 0.5f)]
    public float glowRadius    = 0.18f;
    [Range(0f, 3f)]
    public float glowIntensity = 1.2f;

    // ══════════════════════════════════════════════════════════════════════
    //  STARS
    // ══════════════════════════════════════════════════════════════════════
    [Header("Stars")]
    [Range(0, 30)]
    public int   starCount        = 14;
    [Range(0f, 1f)]
    public float starBrightness   = 0.75f;
    [Range(0f, 5f)]
    public float starTwinkleSpeed = 1.1f;

    // ══════════════════════════════════════════════════════════════════════
    //  INNER GLINT
    // ══════════════════════════════════════════════════════════════════════
    [Header("Inner Glint")]
    [Range(0f, 1f)]
    public float glintIntensity = 0.18f;

    // ── Private ───────────────────────────────────────────────────────────
    private RawImage _rawImage;
    private Material _matInstance;

    static readonly int ID_IllTex    = Shader.PropertyToID("_IllustrationTex");
    static readonly int ID_IllScale  = Shader.PropertyToID("_IllustrationScale");
    static readonly int ID_IllOffX   = Shader.PropertyToID("_IllustrationOffsetX");
    static readonly int ID_IllOffY   = Shader.PropertyToID("_IllustrationOffsetY");
    static readonly int ID_IllAlpha  = Shader.PropertyToID("_IllustrationOpacity");
    static readonly int ID_IllRise   = Shader.PropertyToID("_IllustrationRiseWithWater");

    static readonly int ID_Fill      = Shader.PropertyToID("_FillAmount");
    static readonly int ID_WaveAmp   = Shader.PropertyToID("_WaveAmplitude");
    static readonly int ID_WaveFreq  = Shader.PropertyToID("_WaveFrequency");
    static readonly int ID_WaveSpd   = Shader.PropertyToID("_WaveSpeed");
    static readonly int ID_WaveFreq2 = Shader.PropertyToID("_WaveFrequency2");
    static readonly int ID_WaveSpd2  = Shader.PropertyToID("_WaveSpeed2");

    static readonly int ID_WaterTop  = Shader.PropertyToID("_WaterColorTop");
    static readonly int ID_WaterMid  = Shader.PropertyToID("_WaterColorMid");
    static readonly int ID_WaterBot  = Shader.PropertyToID("_WaterColorBot");
    static readonly int ID_Foam      = Shader.PropertyToID("_FoamColor");
    static readonly int ID_FoamThick = Shader.PropertyToID("_FoamThickness");

    static readonly int ID_BgInner   = Shader.PropertyToID("_BgColorInner");
    static readonly int ID_BgOuter   = Shader.PropertyToID("_BgColorOuter");

    static readonly int ID_BlobR     = Shader.PropertyToID("_BlobRadius");
    static readonly int ID_BlobF1    = Shader.PropertyToID("_BlobNoise1");
    static readonly int ID_BlobA1    = Shader.PropertyToID("_BlobNoise1Amp");
    static readonly int ID_BlobF2    = Shader.PropertyToID("_BlobNoise2");
    static readonly int ID_BlobA2    = Shader.PropertyToID("_BlobNoise2Amp");

    static readonly int ID_RimCol    = Shader.PropertyToID("_RimColor");
    static readonly int ID_RimW      = Shader.PropertyToID("_RimWidth");
    static readonly int ID_GlowCol   = Shader.PropertyToID("_GlowColor");
    static readonly int ID_GlowR     = Shader.PropertyToID("_GlowRadius");
    static readonly int ID_GlowI     = Shader.PropertyToID("_GlowIntensity");

    static readonly int ID_StarN     = Shader.PropertyToID("_StarCount");
    static readonly int ID_StarB     = Shader.PropertyToID("_StarBrightness");
    static readonly int ID_StarT     = Shader.PropertyToID("_StarTwinkleSpeed");
    static readonly int ID_Glint     = Shader.PropertyToID("_GlintIntensity");

    void OnEnable()
    {
        _rawImage = GetComponent<RawImage>();
        if (_rawImage.material == null) return;

        _matInstance       = new Material(_rawImage.material);
        _matInstance.name  = _rawImage.material.name + " (Instance)";
        _rawImage.material = _matInstance;

        PushAll();
    }

    void OnDisable()
    {
        if (_matInstance != null)
        {
            if (Application.isPlaying) Destroy(_matInstance);
            else                       DestroyImmediate(_matInstance);
            _matInstance = null;
        }
    }

    void Update() => PushAll();

    void PushAll()
    {
        if (_matInstance == null) return;

        // Illustration
        if (illustrationTexture != null)
            _matInstance.SetTexture(ID_IllTex, illustrationTexture);
        _matInstance.SetFloat(ID_IllScale,  illustrationScale);
        _matInstance.SetFloat(ID_IllOffX,   illustrationOffsetX);
        _matInstance.SetFloat(ID_IllOffY,   illustrationOffsetY);
        _matInstance.SetFloat(ID_IllAlpha,  illustrationOpacity);
        _matInstance.SetFloat(ID_IllRise,   riseWithWater);

        // Fill & wave
        _matInstance.SetFloat(ID_Fill,      fillAmount);
        _matInstance.SetFloat(ID_WaveAmp,   waveAmplitude);
        _matInstance.SetFloat(ID_WaveFreq,  waveFrequency);
        _matInstance.SetFloat(ID_WaveSpd,   waveSpeed);
        _matInstance.SetFloat(ID_WaveFreq2, waveFrequency2);
        _matInstance.SetFloat(ID_WaveSpd2,  waveSpeed2);

        // Water
        _matInstance.SetColor(ID_WaterTop,  waterColorTop);
        _matInstance.SetColor(ID_WaterMid,  waterColorMid);
        _matInstance.SetColor(ID_WaterBot,  waterColorBot);
        _matInstance.SetColor(ID_Foam,      foamColor);
        _matInstance.SetFloat(ID_FoamThick, foamThickness);

        // BG
        _matInstance.SetColor(ID_BgInner,   bgColorInner);
        _matInstance.SetColor(ID_BgOuter,   bgColorOuter);

        // Blob
        _matInstance.SetFloat(ID_BlobR,     blobRadius);
        _matInstance.SetFloat(ID_BlobF1,    blobNoiseFreq1);
        _matInstance.SetFloat(ID_BlobA1,    blobNoiseAmp1);
        _matInstance.SetFloat(ID_BlobF2,    blobNoiseFreq2);
        _matInstance.SetFloat(ID_BlobA2,    blobNoiseAmp2);

        // Glow & rim
        _matInstance.SetColor(ID_RimCol,    rimColor);
        _matInstance.SetFloat(ID_RimW,      rimWidth);
        _matInstance.SetColor(ID_GlowCol,   glowColor);
        _matInstance.SetFloat(ID_GlowR,     glowRadius);
        _matInstance.SetFloat(ID_GlowI,     glowIntensity);

        // Stars
        _matInstance.SetFloat(ID_StarN,     starCount);
        _matInstance.SetFloat(ID_StarB,     starBrightness);
        _matInstance.SetFloat(ID_StarT,     starTwinkleSpeed);
        _matInstance.SetFloat(ID_Glint,     glintIntensity);
    }

    public void SetFill(float value) => fillAmount = Mathf.Clamp01(value);

    public System.Collections.IEnumerator AnimateFillTo(float target, float duration)
    {
        float start = fillAmount, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFill(Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }
        SetFill(target);
    }
}
