using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Grows a center band outward to a full-strip solid hold.</summary>
public static class WledHttpCenterBandGrowBuilder
{
    public static IReadOnlyList<WledHttpAnimationFrame> Create(
        int ledCount,
        byte brightness,
        RgbColor color,
        int fromLitCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        fromLitCount = NormalizeLitCount(ledCount, fromLitCount);
        if (fromLitCount >= ledCount)
        {
            return
            [
                new WledHttpAnimationFrame(
                    WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                    TimeSpan.Zero)
            ];
        }

        var stepCount = ResolveStepCount(ledCount, fromLitCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        var cadence = TimeSpan.FromMilliseconds(
            WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds);
        for (var step = 0; step < stepCount; step++)
        {
            var litCount = ResolveGrowLitCount(ledCount, fromLitCount, step, stepCount);
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateCenterBand(ledCount, litCount, color, brightness),
                cadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
            TimeSpan.Zero));
        return frames;
    }

    public static int ResolveStepCount(int ledCount, int fromLitCount)
    {
        fromLitCount = Math.Clamp(fromLitCount, 0, ledCount);
        var growEachSide = Math.Max(0, (ledCount - fromLitCount + 1) / 2);
        return Math.Clamp(
            Math.Max(1, growEachSide),
            1,
            WledHttpAnimationFrameFactory.MaximumExpandStepCount);
    }

    private static int NormalizeLitCount(int ledCount, int fromLitCount)
    {
        fromLitCount = Math.Clamp(fromLitCount, ledCount <= 2 ? 1 : 2, ledCount);
        if (ledCount % 2 == 0 && fromLitCount % 2 == 1)
            return Math.Min(ledCount, fromLitCount + 1);
        if (ledCount % 2 == 1 && fromLitCount % 2 == 0)
            return Math.Min(ledCount, fromLitCount + 1);
        return fromLitCount;
    }

    private static int ResolveGrowLitCount(int ledCount, int fromLitCount, int step, int stepCount)
    {
        var fineGrow = Math.Max(0, (ledCount - fromLitCount) / 2);
        if (stepCount >= fineGrow && fineGrow > 0)
            return Math.Clamp(fromLitCount + (step * 2), fromLitCount, ledCount);

        var litCount = (int)Math.Round(
            fromLitCount + ((ledCount - fromLitCount) * ((double)(step + 1) / stepCount)));
        litCount = Math.Clamp(litCount, fromLitCount, ledCount);
        if (ledCount % 2 == 0 && litCount % 2 == 1)
            litCount = Math.Min(ledCount, litCount + 1);
        else if (ledCount % 2 == 1 && litCount % 2 == 0)
            litCount = Math.Min(ledCount, litCount + 1);
        return litCount;
    }
}
