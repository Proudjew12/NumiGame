using UnityEngine;

/// <summary>
/// WaterOrbPreset — ScriptableObject
/// ────────────────────────────────
/// Create presets via:
///   Assets → Create → WaterOrb → Color Preset
///
/// Drag a preset into WaterOrbController.ApplyPreset() or the OrbPresetSwitcher.
/// </summary>
[CreateAssetMenu(menuName = "WaterOrb/Color Preset", fileName = "WaterOrbPreset")]
public class WaterOrbPreset : ScriptableObject
{
    [Header("Water")]
    public Color waterColorTop = new Color(0.62f, 0.57f, 0.94f, 0.75f);
    public Color waterColorMid = new Color(0.47f, 0.39f, 0.86f, 0.85f);
    public Color waterColorBot = new Color(0.27f, 0.22f, 0.67f, 0.95f);
    public Color foamColor     = new Color(0.87f, 0.84f, 1f,    0.50f);

    [Header("Glow & Rim")]
    public Color rimColor      = new Color(0.74f, 0.71f, 1f,    1f);
    public Color glowColor     = new Color(0.47f, 0.43f, 0.90f, 1f);

    [Header("Interior")]
    public Color bgColorInner  = new Color(0.10f, 0.09f, 0.25f, 1f);
    public Color bgColorOuter  = new Color(0.04f, 0.04f, 0.13f, 1f);
}
