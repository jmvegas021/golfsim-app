using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Device;

/// <summary>Builds compact, deterministic Solid FX 0 frames for HTTP state animations.</summary>
public static class WledHttpAnimationFrameFactory
{
    /// <summary>Caps expand POSTs so long strips stay smooth without spamming HTTP.</summary>
    public const int MaximumExpandStepCount = 16;

    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);
    private static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 220, 0);
    private static readonly RgbColor NotReadyRed = RgbColor.FromRgb(180, 30, 30);
    private static readonly RgbColor HitFarRed = RgbColor.FromRgb(220, 40, 40);
    private static readonly RgbColor HitMidYellow = RgbColor.FromRgb(220, 180, 0);

    /// <summary>Brightness factors from 10% up to 100% and back (no zero floor).</summary>
    private static readonly double[] BreathingLevels =
    [
        0.10, 0.18, 0.28, 0.40, 0.55, 0.72, 0.88, 1.0,
        0.88, 0.72, 0.55, 0.40, 0.28, 0.18
    ];

    private static readonly TimeSpan BreathingCadence = TimeSpan.FromMilliseconds(95);
    private static readonly TimeSpan ExpandCadence = TimeSpan.FromMilliseconds(55);

    public static IReadOnlyList<WledHttpAnimationFrame> CreateRedBreathingCycle(byte brightness) =>
        BreathingLevels
            .Select(level => new WledHttpAnimationFrame(
                CreateSolidBody(NotReadyRed, ScaleBrightness(brightness, level)),
                BreathingCadence))
            .ToArray();

    public static IReadOnlyList<WledHttpAnimationFrame> CreateNotReadyExpandSequence(
        int ledCount,
        byte brightness) =>
        CreateCenterOutSequence(ledCount, brightness, NotReadyRed, includeHoldFrame: true);

    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadySequence(int ledCount, byte brightness) =>
        CreateCenterOutSequence(ledCount, brightness, ReadyGreen, includeHoldFrame: true);

    /// <summary>
    /// Hit-direction animation from strip center: left-only, right-only, or center-out.
    /// Holds the final filled pattern (no ambient return).
    /// </summary>
    public static IReadOnlyList<WledHttpAnimationFrame> CreateHitDirectionSequence(
        ShotDirection direction,
        int ledCount,
        byte brightness)
    {
        var color = ResolveHitColor(direction);
        return direction switch
        {
            ShotDirection.FarLeft or ShotDirection.MidLeft =>
                CreateLeftExpandSequence(ledCount, brightness, color),
            ShotDirection.FarRight or ShotDirection.MidRight =>
                CreateRightExpandSequence(ledCount, brightness, color),
            _ => CreateCenterOutSequence(ledCount, brightness, color, includeHoldFrame: true)
        };
    }

    public static RgbColor ResolveHitColor(ShotDirection direction) =>
        direction switch
        {
            ShotDirection.FarLeft or ShotDirection.FarRight => HitFarRed,
            ShotDirection.MidLeft or ShotDirection.MidRight => HitMidYellow,
            _ => ReadyGreen
        };

    private static IReadOnlyList<WledHttpAnimationFrame> CreateLeftExpandSequence(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        // Left half ends at the first right-half index (exclusive): even 12 → 6, odd 11 → 5.
        var rightEdge = (ledCount + 1) / 2;
        var maxLit = Math.Max(1, rightEdge);
        var stepCount = Math.Min(maxLit, MaximumExpandStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveUnilateralLitCount(maxLit, step, stepCount);
            var start = rightEdge - litCount;
            frames.Add(new WledHttpAnimationFrame(
                CreateRangeFillBody(ledCount, start, rightEdge, color, brightness),
                ExpandCadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            CreateRangeFillBody(ledCount, 0, rightEdge, color, brightness),
            TimeSpan.Zero));
        return frames;
    }

    private static IReadOnlyList<WledHttpAnimationFrame> CreateRightExpandSequence(
        int ledCount,
        byte brightness,
        RgbColor color)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        // Right half starts at the first right-half index: even 12 → 6, odd 11 → 5.
        var leftEdge = ledCount / 2;
        var maxLit = Math.Max(1, ledCount - leftEdge);
        var stepCount = Math.Min(maxLit, MaximumExpandStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        for (var step = 1; step <= stepCount; step++)
        {
            var litCount = ResolveUnilateralLitCount(maxLit, step, stepCount);
            var stop = leftEdge + litCount;
            frames.Add(new WledHttpAnimationFrame(
                CreateRangeFillBody(ledCount, leftEdge, stop, color, brightness),
                ExpandCadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            CreateRangeFillBody(ledCount, leftEdge, ledCount, color, brightness),
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
                CreateCenterOutBody(ledCount, litCount, color, brightness),
                ExpandCadence));
        }

        if (includeHoldFrame)
        {
            frames.Add(new WledHttpAnimationFrame(
                CreateFinalSolidBody(ledCount, color, brightness),
                TimeSpan.Zero));
        }

        return frames;
    }

    private static int ResolveSymmetricLitCount(int ledCount, int step, int stepCount)
    {
        var litCount = (int)Math.Ceiling((double)(step * ledCount) / stepCount);
        // Keep the lit block centered: even strips grow in pairs, odd strips keep a true center.
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

    private static object CreateSolidBody(RgbColor color, byte brightness) =>
        new Dictionary<string, object?>
        {
            ["on"] = true,
            ["bri"] = brightness,
            ["live"] = false,
            ["seg"] = new[] { CreateColorSegment(0, color) }
        };

    private static object CreateCenterOutBody(
        int ledCount,
        int litCount,
        RgbColor color,
        byte brightness)
    {
        litCount = Math.Clamp(litCount, 0, ledCount);
        var start = (ledCount - litCount) / 2;
        var stop = start + litCount;
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, start, Black),
                CreateRangeSegment(1, start, stop, color),
                CreateRangeSegment(2, stop, ledCount, Black)
            ]);
    }

    private static object CreateRangeFillBody(
        int ledCount,
        int litStart,
        int litStop,
        RgbColor color,
        byte brightness)
    {
        litStart = Math.Clamp(litStart, 0, ledCount);
        litStop = Math.Clamp(litStop, litStart, ledCount);
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, litStart, Black),
                CreateRangeSegment(1, litStart, litStop, color),
                CreateRangeSegment(2, litStop, ledCount, Black)
            ]);
    }

    private static object CreateFinalSolidBody(int ledCount, RgbColor color, byte brightness) =>
        CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, ledCount, color),
                new Dictionary<string, object?> { ["id"] = 1, ["stop"] = 0 },
                new Dictionary<string, object?> { ["id"] = 2, ["stop"] = 0 }
            ]);

    private static object CreateBody(byte brightness, object[] segments) =>
        new Dictionary<string, object?>
        {
            ["on"] = true,
            ["bri"] = brightness,
            ["live"] = false,
            ["seg"] = segments
        };

    private static Dictionary<string, object?> CreateRangeSegment(
        int id,
        int start,
        int stop,
        RgbColor color)
    {
        var segment = CreateColorSegment(id, color);
        segment["start"] = start;
        segment["stop"] = stop;
        return segment;
    }

    private static Dictionary<string, object?> CreateColorSegment(int id, RgbColor color) =>
        new()
        {
            ["id"] = id,
            ["fx"] = 0,
            ["col"] = new[] { ToRgb(color), ToRgb(Black), ToRgb(Black) }
        };

    private static byte ScaleBrightness(byte brightness, double level) =>
        (byte)Math.Max(1, Math.Round(brightness * level));

    private static int[] ToRgb(RgbColor color) => [color.R, color.G, color.B];
}
