using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds the authoritative Not Ready Chase hold (full-strip Chase + Red Reef).
/// Ready rests on solid geometric HTTP frames — not this factory.
/// </summary>
public static class WledChaseAuroraStateFactory
{
    /// <summary>WLED default FX id for Chase (well-known across stock builds).</summary>
    public const int ChaseFxId = EffectConfig.ChaseFxId;

    /// <summary>WLED built-in palette Aurora (id 50 — greens on dark blue).</summary>
    public const int AuroraPaletteId = EffectConfig.AuroraPaletteId;

    /// <summary>WLED built-in palette Red Reef (id 62 — red gradient).</summary>
    public const int RedReefPaletteId = EffectConfig.RedReefPaletteId;

    /// <summary>Default palette — kept for tests / sparse solid helpers.</summary>
    public const int DefaultPaletteId = WledAuthoritativeStateFactory.DefaultPaletteId;

    public static readonly RgbColor NotReadyPrimary = RgbColor.FromRgb(180, 30, 30);
    public static readonly RgbColor ReadyPrimary = RgbColor.FromRgb(0, 220, 0);

    /// <summary>Full-strip red Chase + Red Reef at max sx/ix — no Aurora.</summary>
    public static object CreateNotReadyBody(int ledCount, byte brightness) =>
        WledAuthoritativeStateFactory.CreateFullStripBody(
            ledCount,
            brightness,
            ChaseFxId,
            EffectConfig.MaxTimingByte,
            EffectConfig.MaxTimingByte,
            RedReefPaletteId,
            NotReadyPrimary);
}
