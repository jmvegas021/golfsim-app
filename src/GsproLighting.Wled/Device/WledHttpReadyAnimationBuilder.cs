using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Ready: edges→center fill, then chase collapse to a lit center band.
/// </summary>
public static class WledHttpReadyAnimationBuilder
{
    /// <summary>Fraction of the strip that remains lit after the Ready chase.</summary>
    public const double HoldLitFraction = 0.28;

    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadySequence(
        int ledCount,
        byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var color = WledHttpAnimationFrameFactory.ReadyGreen;
        var frames = new List<WledHttpAnimationFrame>(
            WledHttpAnimationFrameFactory.MaximumExpandStepCount * 2 + 1);
        frames.AddRange(CreateEdgesInFrames(ledCount, brightness, color));
        frames.AddRange(CreateChaseToCenterFrames(ledCount, brightness, color, includeFullStart: false));
        return frames;
    }

    /// <summary>
    /// When the strip is already fully lit (e.g. after morphing from Not Ready),
    /// skip edges-in and chase from full down to the center hold band.
    /// </summary>
    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadyChaseFromFullSequence(
        int ledCount,
        byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        return CreateChaseToCenterFrames(
            ledCount,
            brightness,
            WledHttpAnimationFrameFactory.ReadyGreen,
            includeFullStart: true);
    }

    public static int ResolveHoldLitCount(int ledCount)
    {
        var raw = (int)Math.Round(ledCount * HoldLitFraction);
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
        var holdLit = ResolveHoldLitCount(ledCount);
        var shrinkEachSide = Math.Max(0, (ledCount - holdLit) / 2);
        return Math.Clamp(Math.Max(1, shrinkEachSide), 1, WledHttpAnimationFrameFactory.MaximumExpandStepCount);
    }

    private static IReadOnlyList<WledHttpAnimationFrame> CreateEdgesInFrames(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        var stepCount = ResolveEdgesInStepCount(ledCount);
        var maxFromEdge = (ledCount + 1) / 2;
        var frames = new List<WledHttpAnimationFrame>(stepCount);
        for (var step = 1; step <= stepCount; step++)
        {
            var litEach = ResolveStepLit(maxFromEdge, step, stepCount);
            var leftStop = litEach;
            var rightStart = ledCount - litEach;
            var duration = step == stepCount
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds);
            if (leftStop >= rightStart)
            {
                frames.Add(new WledHttpAnimationFrame(
                    WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                    duration));
            }
            else
            {
                frames.Add(new WledHttpAnimationFrame(
                    WledHttpSegmentBodies.CreateEdgesIn(ledCount, leftStop, rightStart, color, brightness),
                    duration));
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
        var holdLit = ResolveHoldLitCount(ledCount);
        var stepCount = ResolveChaseStepCount(ledCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        if (includeFullStart)
        {
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds)));
        }

        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveChaseLitCount(ledCount, holdLit, step, stepCount);
            var duration = step == stepCount
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds);
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateCenterBand(ledCount, litCount, color, brightness),
                duration));
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

    private static int ResolveChaseLitCount(int ledCount, int holdLit, int step, int stepCount)
    {
        var fineShrink = Math.Max(0, (ledCount - holdLit) / 2);
        if (stepCount >= fineShrink && fineShrink > 0)
        {
            var litCount = ledCount - (step * 2);
            return Math.Clamp(litCount, holdLit, ledCount);
        }

        var litCountCoarse = (int)Math.Round(ledCount + ((holdLit - ledCount) * ((double)step / stepCount)));
        litCountCoarse = Math.Clamp(litCountCoarse, holdLit, ledCount);
        if (ledCount % 2 == 0 && litCountCoarse % 2 == 1)
            litCountCoarse = Math.Max(holdLit, litCountCoarse - 1);
        else if (ledCount % 2 == 1 && litCountCoarse % 2 == 0)
            litCountCoarse = Math.Max(holdLit, litCountCoarse - 1);
        return litCountCoarse;
    }
}
