using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Cross-checks the hardcoded product WLED FX/palette ids in <see cref="EffectConfig"/> against
/// a device's actual <c>/json/eff</c> and <c>/json/pal</c> catalogs (device catalogs vary by
/// firmware/build). Returns human-readable, non-blocking warnings — never throws.
/// </summary>
public static class WledCatalogValidator
{
    public static IReadOnlyList<string> ValidateConfiguredIds(
        IReadOnlyList<WledNamedEntry> effects,
        IReadOnlyList<WledNamedEntry> palettes)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(palettes);

        var warnings = new List<string>();

        CheckEffect(effects, EffectConfig.RippleFxId, "Ripple ambient (Idle/Waiting)", warnings);
        CheckEffect(effects, EffectConfig.ChaseFxId, "Chase (Ready / Not Ready)", warnings);
        CheckEffect(effects, EffectConfig.CelebrateFxId, "Celebrate", warnings);
        CheckEffect(effects, EffectConfig.SparkleFxId, "Hazard", warnings);
        CheckPalette(palettes, EffectConfig.RedReefPaletteId, "Red Reef (ambient)", warnings);
        CheckPalette(palettes, EffectConfig.AuroraPaletteId, "Aurora (Ready)", warnings);

        return warnings;
    }

    private static void CheckEffect(
        IReadOnlyList<WledNamedEntry> effects,
        int fxId,
        string label,
        List<string> warnings)
    {
        if (effects.Count == 0)
            return;

        if (!effects.Any(e => e.Id == fxId))
            warnings.Add($"{label} effect (FX {fxId}) not found on this device.");
    }

    private static void CheckPalette(
        IReadOnlyList<WledNamedEntry> palettes,
        int paletteId,
        string label,
        List<string> warnings)
    {
        if (palettes.Count == 0)
            return;

        if (!palettes.Any(p => p.Id == paletteId))
            warnings.Add($"{label} palette ({paletteId}) not found on this device.");
    }
}
