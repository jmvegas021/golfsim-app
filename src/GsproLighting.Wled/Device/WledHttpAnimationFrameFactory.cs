using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Device;

/// <summary>Builds compact, deterministic Solid FX 0 frames for HTTP state animations.</summary>
public static class WledHttpAnimationFrameFactory
{
    /// <summary>Caps expand POSTs so long strips stay smooth without spamming HTTP.</summary>
    public const int MaximumExpandStepCount = 16;

    /// <summary>HTTP frames when morphing from one solid color/brightness to another.</summary>
    public const int ColorTransitionStepCount = 10;

    public const int ExpandCadenceMilliseconds = 42;
    public const int BreathingCadenceMilliseconds = 72;
    public const int ColorTransitionCadenceMilliseconds = 40;

    public static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 220, 0);
    public static readonly RgbColor NotReadyRed = RgbColor.FromRgb(180, 30, 30);
    public static readonly RgbColor HitSideYellow = RgbColor.FromRgb(220, 180, 0);

    /// <summary>
    /// Brightness factors for one breathe loop: 100% → 10% → just below 100%.
    /// The next cycle starts at 100% again so seams stay seamless (no 0 flash).
    /// </summary>
    private static readonly double[] BreathingLevels =
    [
        1.0, 0.88, 0.72, 0.55, 0.40, 0.28, 0.18, 0.10,
        0.18, 0.28, 0.40, 0.55, 0.72, 0.88
    ];

    private static readonly TimeSpan BreathingCadence =
        TimeSpan.FromMilliseconds(BreathingCadenceMilliseconds);
    private static readonly TimeSpan ExpandCadence =
        TimeSpan.FromMilliseconds(ExpandCadenceMilliseconds);
    private static readonly TimeSpan ColorTransitionCadence =
        TimeSpan.FromMilliseconds(ColorTransitionCadenceMilliseconds);

    public static IReadOnlyList<WledHttpAnimationFrame> CreateRedBreathingCycle(byte brightness) =>
        CreateRedBreathingTracked(brightness).Select(step => step.Frame).ToArray();

    public static IReadOnlyList<WledHttpAnimationFrame> CreateColorTransitionSequence(
        RgbColor fromColor,
        byte fromBrightness,
        RgbColor toColor,
        byte toBrightness) =>
        CreateColorTransitionTracked(fromColor, fromBrightness, toColor, toBrightness)
            .Select(step => step.Frame)
            .ToArray();

    public static IReadOnlyList<WledHttpTrackedFrame> CreateColorTransitionTracked(
        RgbColor fromColor,
        byte fromBrightness,
        RgbColor toColor,
        byte toBrightness)
    {
        var frames = new List<WledHttpTrackedFrame>(ColorTransitionStepCount);
        for (var step = 1; step <= ColorTransitionStepCount; step++)
        {
            var t = (double)step / ColorTransitionStepCount;
            var color = LerpColor(fromColor, toColor, t);
            var brightness = LerpBrightness(fromBrightness, toBrightness, t);
            var duration = step == ColorTransitionStepCount ? TimeSpan.Zero : ColorTransitionCadence;
            frames.Add(new WledHttpTrackedFrame(
                new WledHttpAnimationFrame(WledHttpSegmentBodies.CreateSolid(color, brightness), duration),
                color,
                brightness));
        }

        return frames;
    }

    public static IReadOnlyList<WledHttpTrackedFrame> CreateRedBreathingTracked(byte brightness) =>
        BreathingLevels
            .Select(level =>
            {
                var frameBrightness = ScaleBrightness(brightness, level);
                return new WledHttpTrackedFrame(
                    new WledHttpAnimationFrame(
                        WledHttpSegmentBodies.CreateSolid(NotReadyRed, frameBrightness),
                        BreathingCadence),
                    NotReadyRed,
                    frameBrightness);
            })
            .ToArray();

    public static IReadOnlyList<WledHttpAnimationFrame> CreateNotReadyExpandSequence(
        int ledCount,
        byte brightness) =>
        CreateCenterOutSequence(ledCount, brightness, NotReadyRed, includeHoldFrame: true);

    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadySequence(int ledCount, byte brightness) =>
        WledHttpReadyAnimationBuilder.CreateReadySequence(ledCount, brightness);

    /// <summary>
    /// Hit-direction: left/right half-fill from center, or full center-out green.
    /// </summary>
    public static IReadOnlyList<WledHttpAnimationFrame> CreateHitDirectionSequence(
        ShotDirection direction,
        int ledCount,
        byte brightness)
    {
        var color = ResolveHitColor(direction);
        return direction switch
        {
            ShotDirection.Left => CreateLeftHalfSequence(ledCount, brightness, color),
            ShotDirection.Right => CreateRightHalfSequence(ledCount, brightness, color),
            _ => CreateCenterOutSequence(ledCount, brightness, color, includeHoldFrame: true)
        };
    }

    public static RgbColor ResolveHitColor(ShotDirection direction) =>
        direction switch
        {
            ShotDirection.Left or ShotDirection.Right => HitSideYellow,
            _ => ReadyGreen
        };

    private static IReadOnlyList<WledHttpAnimationFrame> CreateLeftHalfSequence(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        // Left half ends at the first right-half index (exclusive): even 12 → 6, odd 11 → 5.
        var rightEdge = (ledCount + 1) / 2;
        var maxLit = Math.Max(1, rightEdge);
        var stepCount = Math.Min(ledCount, MaximumExpandStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveUnilateralLitCount(maxLit, step, stepCount);
            var start = rightEdge - litCount;
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateRangeFill(ledCount, start, rightEdge, color, brightness),
                ExpandCadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            WledHttpSegmentBodies.CreateRangeFill(ledCount, 0, rightEdge, color, brightness),
            TimeSpan.Zero));
        return frames;
    }

    private static IReadOnlyList<WledHttpAnimationFrame> CreateRightHalfSequence(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var leftEdge = ledCount / 2;
        var maxLit = Math.Max(1, ledCount - leftEdge);
        var stepCount = Math.Min(ledCount, MaximumExpandStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveUnilateralLitCount(maxLit, step, stepCount);
            var stop = leftEdge + litCount;
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateRangeFill(ledCount, leftEdge, stop, color, brightness),
                ExpandCadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            WledHttpSegmentBodies.CreateRangeFill(ledCount, leftEdge, ledCount, color, brightness),
            TimeSpan.Zero));
        return frames;
    }

    private static IReadOnlyList<WledHttpAnimationFrame> CreateCenterOutSequence(
        int ledCount,
        byte brightness,
        RgbColor color,
        bool includeHoldFrame)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var stepCount = Math.Min(ledCount, MaximumExpandStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + (includeHoldFrame ? 1 : 0));
        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveSymmetricLitCount(ledCount, step, stepCount);
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateCenterBand(ledCount, litCount, color, brightness),
                ExpandCadence));
        }

        if (includeHoldFrame)
        {
            frames.Add(new WledHttpAnimationFrame(
                WledHttpSegmentBodies.CreateFullStrip(ledCount, color, brightness),
                TimeSpan.Zero));
        }

        return frames;
    }

    private static int ResolveSymmetricLitCount(int ledCount, int step, int stepCount)
    {
        var litCount = (int)Math.Ceiling((double)(step * ledCount) / stepCount);
        if (ledCount % 2 == 0 && litCount % 2 == 1)
            litCount = Math.Min(ledCount, litCount + 1);
        else if (ledCount % 2 == 1 && litCount % 2 == 0)
            litCount = Math.Min(ledCount, litCount + 1);
        return litCount;
    }

    private static int ResolveUnilateralLitCount(int maxLit, int step, int stepCount)
    {
        var litCount = (int)Math.Ceiling((double)(step * maxLit) / stepCount);
        return Math.Clamp(litCount, 1, maxLit);
    }

    private static RgbColor LerpColor(RgbColor from, RgbColor to, double t) =>
        RgbColor.FromRgb(
            LerpBrightness(from.R, to.R, t),
            LerpBrightness(from.G, to.G, t),
            LerpBrightness(from.B, to.B, t));

    private static byte LerpBrightness(byte from, byte to, double t) =>
        (byte)Math.Round(from + ((to - from) * t));

    private static byte ScaleBrightness(byte brightness, double level) =>
        (byte)Math.Max(1, Math.Round(brightness * level));
}
