using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Ready: edges→center, concentrate to a ~50% center band, then full-strip solid green.
/// </summary>
public static class WledHttpReadyAnimationBuilder
{
    /// <summary>Fraction of the strip lit after the Ready concentrate beat.</summary>
    public const double ConcentrateLitFraction = 0.50;

    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadySequence(
        int ledCount,
        byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var color = WledHttpAnimationFrameFactory.ReadyGreen;
        var frames = new List<WledHttpAnimationFrame>(
            WledHttpAnimationFrameFactory.MaximumExpandStepCount * 3 + 1);
        frames.AddRange(CreateEdgesInFrames(ledCount, brightness, color));
        frames.AddRange(CreateChaseToCenterFrames(ledCount, brightness, color, includeFullStart: false));
        frames.AddRange(
            WledHttpAnimationFrameFactory.CreateCenterBandGrowSequence(
                ledCount,
                brightness,
                color,
                ResolveConcentrateLitCount(ledCount)));
        return frames;
    }

    /// <summary>
    /// When the strip is already fully lit (e.g. after morphing from Not Ready),
    /// skip edges-in: chase to the center concentrate band, then grow to full solid.
    /// </summary>
    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadyChaseFromFullSequence(
        int ledCount,
        byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var color = WledHttpAnimationFrameFactory.ReadyGreen;
        var frames = new List<WledHttpAnimationFrame>(
            WledHttpAnimationFrameFactory.MaximumExpandStepCount * 2 + 2);
        frames.AddRange(CreateChaseToCenterFrames(ledCount, brightness, color, includeFullStart: true));
        frames.AddRange(
            WledHttpAnimationFrameFactory.CreateCenterBandGrowSequence(
                ledCount,
                brightness,
                color,
                ResolveConcentrateLitCount(ledCount)));
        return frames;
    }

    public static int ResolveConcentrateLitCount(int ledCount)
    {
        var raw = (int)Math.Round(ledCount * ConcentrateLitFraction);
        var hold = Math.Clamp(raw, ledCount <= 2 ? 1 : 2, ledCount);
        if (ledCount % 2 == 0 && hold % 2 == 1)
            hold = Math.Min(ledCount, hold + 1);
        else if (ledCount % 2 == 1 && hold % 2 == 0)
            hold = Math.Min(ledCount, hold + 1);
        return hold;
    }

    /// <summary>Unique edges-in frames: ~1 LED per edge per step when under the cap.</summary>
    public static int ResolveEdgesInStepCount(int ledCount) =>
        WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount);

    /// <summary>Unique chase frames shrinking by ~1 LED per side when under the cap.</summary>
    public static int ResolveChaseStepCount(int ledCount)
    {
        var concentrateLit = ResolveConcentrateLitCount(ledCount);
        var shrinkEachSide = Math.Max(0, (ledCount - concentrateLit) / 2);
        return Math.Clamp(
            Math.Max(1, shrinkEachSide),
            1,
            WledHttpAnimationFrameFactory.MaximumExpandStepCount);
    }

    public static int ResolveExpandStepCount(int ledCount) =>
        WledHttpCenterBandGrowBuilder.ResolveStepCount(
            ledCount,
            ResolveConcentrateLitCount(ledCount));

    private static IReadOnlyList<WledHttpAnimationFrame> CreateEdgesInFrames(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        var stepCount = ResolveEdgesInStepCount(ledCount);
        var maxFromEdge = (ledCount + 1) / 2;
        var frames = new List<WledHttpAnimationFrame>(stepCount);
        var cadence = TimeSpan.FromMilliseconds(
            WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds);
        for (var step = 1; step <= stepCount; step++)
        {
            var litEach = ResolveStepLit(maxFromEdge, step, stepCount);
            var leftStop = litEach;
            var rightStart = ledCount - litEach;
            if (leftStop >= rightStart)
            {
                frames.Add(new WledHttpAnimationFrame(
                    WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                    cadence));
            }
            else
            {
                frames.Add(new WledHttpAnimationFrame(
                    WledHttpSegmentBodies.CreateEdgesIn(
                        ledCount,
                        leftStop,
                        rightStart,
                        color,
                        brightness),
                    cadence));
            }
        }

        return frames;
    }

    private static IReadOnlyList<WledHttpAnimationFrame> CreateChaseToCenterFrames(
        int ledCount,
        byte brightness,
        RgbColor color,
        bool includeFullStart)
    {
        var concentrateLit = ResolveConcentrateLitCount(ledCount);
        var stepCount = ResolveChaseStepCount(ledCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        var cadence = TimeSpan.FromMilliseconds(
            WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds);
        if (includeFullStart)
        {
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                cadence));
        }

        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveChaseLitCount(ledCount, concentrateLit, step, stepCount);
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateCenterBand(ledCount, litCount, color, brightness),
                cadence));
        }

        return frames;
    }

    private static int ResolveStepLit(int maxLit, int step, int stepCount)
    {
        if (stepCount >= maxLit)
            return Math.Clamp(step, 1, maxLit);

        var litCount = (int)Math.Ceiling((double)(step * maxLit) / stepCount);
        return Math.Clamp(litCount, 1, maxLit);
    }

    private static int ResolveChaseLitCount(int ledCount, int concentrateLit, int step, int stepCount)
    {
        var fineShrink = Math.Max(0, (ledCount - concentrateLit) / 2);
        if (stepCount >= fineShrink && fineShrink > 0)
        {
            var litCount = ledCount - (step * 2);
            return Math.Clamp(litCount, concentrateLit, ledCount);
        }

        var litCountCoarse = (int)Math.Round(
            ledCount + ((concentrateLit - ledCount) * ((double)step / stepCount)));
        litCountCoarse = Math.Clamp(litCountCoarse, concentrateLit, ledCount);
        if (ledCount % 2 == 0 && litCountCoarse % 2 == 1)
            litCountCoarse = Math.Max(concentrateLit, litCountCoarse - 1);
        else if (ledCount % 2 == 1 && litCountCoarse % 2 == 0)
            litCountCoarse = Math.Max(concentrateLit, litCountCoarse - 1);
        return litCountCoarse;
    }
}
