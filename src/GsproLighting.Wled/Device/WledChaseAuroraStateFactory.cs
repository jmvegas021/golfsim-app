using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Builds full-strip native Chase state bodies for Ready / Not Ready.
/// Ready uses Aurora palette; Not Ready stays solid red (Default palette).
/// Clears leftover multi-segment geometry the same way as
/// <see cref="WledHttpSegmentBodies.CreateFullStrip"/>.
/// </summary>
public static class WledChaseAuroraStateFactory
{
    /// <summary>WLED default FX id for Chase (well-known across stock builds).</summary>
    public const int ChaseFxId = EffectConfig.ChaseFxId;

    /// <summary>WLED built-in palette Aurora (id 50 — greens on dark blue).</summary>
    public const int AuroraPaletteId = EffectConfig.AuroraPaletteId;

    /// <summary>Default palette — lets segment <c>col</c> drive a red Chase.</summary>
    public const int DefaultPaletteId = 0;

    public static readonly RgbColor NotReadyPrimary = RgbColor.FromRgb(180, 30, 30);
    public static readonly RgbColor ReadyPrimary = RgbColor.FromRgb(0, 220, 0);

    /// <summary>Red Chase at max sx/ix — no Aurora.</summary>
    public static object CreateNotReadyBody(int ledCount, byte brightness) =>
        CreateFullStripBody(
            ledCount,
            brightness,
            NotReadyPrimary,
            DefaultPaletteId);

    /// <summary>Chase + Aurora palette at max sx/ix.</summary>
    public static object CreateReadyBody(int ledCount, byte brightness) =>
        CreateFullStripBody(
            ledCount,
            brightness,
            ReadyPrimary,
            AuroraPaletteId);

    public static object CreateFullStripBody(
        int ledCount,
        byte brightness,
        RgbColor primary,
        int paletteId)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        ArgumentNullException.ThrowIfNull(primary);

        return new Dictionary<string, object?>
        {
            ["on"] = true,
            ["bri"] = brightness,
            ["live"] = false,
            ["seg"] = new object[]
            {
                CreateChaseSegment(0, 0, ledCount, primary, paletteId),
                new Dictionary<string, object?> { ["id"] = 1, ["stop"] = 0 },
                new Dictionary<string, object?> { ["id"] = 2, ["stop"] = 0 }
            }
        };
    }

    private static Dictionary<string, object?> CreateChaseSegment(
        int id,
        int start,
        int stop,
        RgbColor primary,
        int paletteId) =>
        new()
        {
            ["id"] = id,
            ["start"] = start,
            ["stop"] = stop,
            ["fx"] = ChaseFxId,
            ["sx"] = EffectConfig.MaxTimingByte,
            ["ix"] = EffectConfig.MaxTimingByte,
            ["pal"] = paletteId,
            ["col"] = new[]
            {
                ToRgb(primary),
                ToRgb(RgbColor.FromRgb(0, 0, 0)),
                ToRgb(RgbColor.FromRgb(0, 0, 0))
            }
        };

    private static int[] ToRgb(RgbColor color) => [color.R, color.G, color.B];
}
