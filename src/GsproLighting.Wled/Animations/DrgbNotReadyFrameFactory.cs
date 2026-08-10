using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame Not Ready choreography for DRGB: optional green→red morph on the Ready
/// center band, center→sides expand to full red, then brightness breathe keepalive.
/// </summary>
public static class DrgbNotReadyFrameFactory
{
    public const int FrameCadenceMilliseconds = DrgbReadyFrameFactory.FrameCadenceMilliseconds;
    public const int MorphStepCount = 10;
    public const int BreathingCadenceMilliseconds =
        WledHttpAnimationFrameFactory.BreathingCadenceMilliseconds;

    public static readonly RgbColor NotReadyRed = WledHttpAnimationFrameFactory.NotReadyRed;

    /// <summary>
    /// Brightness factors for one breathe loop: 100% → 10% → just below 100%.
    /// The next cycle starts at 100% again so seams stay seamless.
    /// </summary>
    public static readonly IReadOnlyList<double> BreathingLevels =
    [
        1.0, 0.88, 0.72, 0.55, 0.40, 0.28, 0.18, 0.10,
        0.18, 0.28, 0.40, 0.55, 0.72, 0.88
    ];

    public static IReadOnlyList<LedAnimationFrame> CreateFromReadyCenterBand(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        var frames = new List<LedAnimationFrame>(MorphStepCount + ((ledCount - concentrate) / 2) + 1);

        for (var step = 1; step <= MorphStepCount; step++)
        {
            var t = (double)step / MorphStepCount;
            var color = Lerp(DrgbReadyFrameFactory.ReadyGreen, NotReadyRed, t);
            var duration = step == MorphStepCount && concentrate >= ledCount
                ? TimeSpan.Zero
                : cadence;
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateCenterBand(ledCount, concentrate, color),
                duration));
        }

        AppendExpandFrames(frames, ledCount, concentrate, cadence);
        return frames;
    }

    public static IReadOnlyList<LedAnimationFrame> CreateExpandFromDark(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var stepCount = (ledCount + 1) / 2;
        var frames = new List<LedAnimationFrame>(stepCount + 1);
        AppendExpandFrames(frames, ledCount, fromLitCount: 0, cadence);
        return frames;
    }

    public static byte ScaleBrightness(byte brightness, double level) =>
        (byte)Math.Max(1, Math.Round(brightness * level));

    private static void AppendExpandFrames(
        List<LedAnimationFrame> frames,
        int ledCount,
        int fromLitCount,
        TimeSpan cadence)
    {
        var parity = ledCount % 2 == 0 ? 2 : 1;
        var lit = fromLitCount <= 0 ? parity : fromLitCount;
        lit = Math.Clamp(lit, parity, ledCount);

        if (fromLitCount <= 0)
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateCenterBand(ledCount, lit, NotReadyRed),
                cadence));

        while (lit < ledCount)
        {
            lit = Math.Min(ledCount, lit + 2);
            var duration = lit >= ledCount ? TimeSpan.Zero : cadence;
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateCenterBand(ledCount, lit, NotReadyRed),
                duration));
        }

        if (frames.Count == 0 || frames[^1].Duration != TimeSpan.Zero)
        {
            frames.Add(new LedAnimationFrame(
                AnimationPixels.Solid(ledCount, NotReadyRed),
                TimeSpan.Zero));
        }
    }

    private static RgbColor Lerp(RgbColor from, RgbColor to, double t) =>
        RgbColor.FromRgb(
            (byte)Math.Round(from.R + ((to.R - from.R) * t)),
            (byte)Math.Round(from.G + ((to.G - from.G) * t)),
            (byte)Math.Round(from.B + ((to.B - from.B) * t)));
}
