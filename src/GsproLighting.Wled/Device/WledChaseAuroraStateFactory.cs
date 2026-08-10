using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds authoritative full-strip Chase state bodies for Ready / Not Ready.
/// Ready uses Aurora palette; Not Ready stays solid red (Default palette).
/// </summary>
public static class WledChaseAuroraStateFactory
{
    /// <summary>WLED default FX id for Chase (well-known across stock builds).</summary>
    public const int ChaseFxId = EffectConfig.ChaseFxId;

    /// <summary>WLED built-in palette Aurora (id 50 — greens on dark blue).</summary>
    public const int AuroraPaletteId = EffectConfig.AuroraPaletteId;

    /// <summary>Default palette — lets segment <c>col</c> drive a red Chase.</summary>
    public const int DefaultPaletteId = WledAuthoritativeStateFactory.DefaultPaletteId;

    public static readonly RgbColor NotReadyPrimary = RgbColor.FromRgb(180, 30, 30);
    public static readonly RgbColor ReadyPrimary = RgbColor.FromRgb(0, 220, 0);

    /// <summary>Red Chase at max sx/ix — no Aurora.</summary>
    public static object CreateNotReadyBody(int ledCount, byte brightness) =>
        WledAuthoritativeStateFactory.CreateFullStripBody(
            ledCount,
            brightness,
            ChaseFxId,
            EffectConfig.MaxTimingByte,
            EffectConfig.MaxTimingByte,
            DefaultPaletteId,
            NotReadyPrimary);

    /// <summary>Chase + Aurora palette at max sx/ix.</summary>
    public static object CreateReadyBody(int ledCount, byte brightness) =>
        WledAuthoritativeStateFactory.CreateFullStripBody(
            ledCount,
            brightness,
            ChaseFxId,
            EffectConfig.MaxTimingByte,
            EffectConfig.MaxTimingByte,
            AuroraPaletteId,
            ReadyPrimary);
}
