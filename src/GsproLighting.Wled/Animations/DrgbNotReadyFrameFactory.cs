using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame Not Ready choreography for DDP: optional green→red morph on the Ready
/// hold, then center→sides expand to full red; hold shimmer is owned by
/// <see cref="DrgbBandShimmerEffect"/>.
/// </summary>
public static class DrgbNotReadyFrameFactory
{
    public const int FrameCadenceMilliseconds = DrgbReadyFrameFactory.FrameCadenceMilliseconds;
    public const int MorphStepCount = 3;
    public const int LitAdvancePerFrame = DrgbReadyFrameFactory.LitAdvancePerFrame;

    /// <summary>Solid red at full intensity for DDP Not Ready.</summary>
    public static readonly RgbColor NotReadyRed = RgbColor.FromRgb(255, 0, 0);

    public static IReadOnlyList<LedAnimationFrame> CreateFromReadyCenterBand(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var advance = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount, p.IntroSpeedScale);
        var concentrate = DrgbConcentrateBandGeometry.ResolveLitCount(
            ledCount,
            p.ConcentrateLitFraction);
        var expandSteps = Math.Max(0, (ledCount - concentrate) / (2 * advance));
        var frames = new List<LedAnimationFrame>(MorphStepCount + expandSteps + 1);

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

        AppendExpandFrames(frames, ledCount, concentrate, cadence, p.IntroSpeedScale);
        return frames;
    }

    public static IReadOnlyList<LedAnimationFrame> CreateExpandFromDark(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var advance = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount, p.IntroSpeedScale);
        var stepCount = ((ledCount + 1) / 2 + advance - 1) / advance;
        var frames = new List<LedAnimationFrame>(stepCount + 1);
        AppendExpandFrames(frames, ledCount, fromLitCount: 0, cadence, p.IntroSpeedScale);
        return frames;
    }

    public static RgbColor[] CreateHoldPixels(int ledCount) =>
        AnimationPixels.Solid(ledCount, NotReadyRed);

    private static void AppendExpandFrames(
        List<LedAnimationFrame> frames,
        int ledCount,
        int fromLitCount,
        TimeSpan cadence,
        double introSpeedScale = 1.0)
    {
        var parity = ledCount % 2 == 0 ? 2 : 1;
        var lit = fromLitCount <= 0 ? parity : fromLitCount;
        lit = Math.Clamp(lit, parity, ledCount);
        var step = DrgbReadyFrameFactory.ResolveLitAdvance(ledCount, introSpeedScale);
        var advance = Math.Max(2, step * 2);

        if (fromLitCount <= 0)
            frames.Add(new LedAnimationFrame(
                DrgbReadyFrameFactory.CreateCenterBand(ledCount, lit, NotReadyRed),
                cadence));

        while (lit < ledCount)
        {
            lit = Math.Min(ledCount, lit + advance);
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
