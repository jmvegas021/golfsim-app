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

    private static IReadOnlyList<WledHttpAnimationFrame> CreateEdgesInFrames(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        var stepCount = Math.Min(ledCount, WledHttpAnimationFrameFactory.MaximumExpandStepCount);
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
        var stepCount = Math.Min(ledCount, WledHttpAnimationFrameFactory.MaximumExpandStepCount);
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
        var litCount = (int)Math.Ceiling((double)(step * maxLit) / stepCount);
        return Math.Clamp(litCount, 1, maxLit);
    }

    private static int ResolveChaseLitCount(int ledCount, int holdLit, int step, int stepCount)
    {
        var litCount = (int)Math.Round(ledCount + ((holdLit - ledCount) * ((double)step / stepCount)));
        litCount = Math.Clamp(litCount, holdLit, ledCount);
        if (ledCount % 2 == 0 && litCount % 2 == 1)
            litCount = Math.Max(holdLit, litCount - 1);
        else if (ledCount % 2 == 1 && litCount % 2 == 0)
            litCount = Math.Max(holdLit, litCount - 1);
        return litCount;
    }
}
