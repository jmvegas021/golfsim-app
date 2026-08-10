using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds authoritative Chase hold bodies for Ready / Not Ready.
/// Ready: Chase + Aurora on a center band (black sides). Not Ready: full-strip Chase + Red Reef.
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

    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);

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

    /// <summary>
    /// Chase + Aurora at max sx/ix on the Ready concentrate center band;
    /// solid black flanks leave only the top/center portion lit.
    /// </summary>
    public static object CreateReadyBody(int ledCount, byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var litCount = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        var start = (ledCount - litCount) / 2;
        var stop = start + litCount;
        return WledAuthoritativeStateFactory.CreateBody(
            brightness,
            [
                CreateSolidSegment(id: 0, start: 0, stop: start, Black),
                CreateChaseSegment(
                    id: 1,
                    start,
                    stop,
                    ChaseFxId,
                    AuroraPaletteId,
                    ReadyPrimary),
                CreateSolidSegment(id: 2, start: stop, stop: ledCount, Black)
            ],
            mainSegmentId: 1);
    }

    private static Dictionary<string, object?> CreateSolidSegment(
        int id,
        int start,
        int stop,
        RgbColor color) =>
        WledAuthoritativeStateFactory.CreateSegment(
            id,
            start,
            stop,
            WledAuthoritativeStateFactory.SolidFxId,
            WledAuthoritativeStateFactory.DefaultTimingByte,
            WledAuthoritativeStateFactory.DefaultTimingByte,
            DefaultPaletteId,
            color);

    private static Dictionary<string, object?> CreateChaseSegment(
        int id,
        int start,
        int stop,
        int fxId,
        int paletteId,
        RgbColor primary) =>
        WledAuthoritativeStateFactory.CreateSegment(
            id,
            start,
            stop,
            fxId,
            EffectConfig.MaxTimingByte,
            EffectConfig.MaxTimingByte,
            paletteId,
            primary);
}
