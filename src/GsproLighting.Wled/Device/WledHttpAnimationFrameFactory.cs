using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Builds compact, deterministic Solid FX 0 frames for HTTP state animations.</summary>
public static class WledHttpAnimationFrameFactory
{
    /// <summary>Caps expand POSTs so long strips stay smooth without spamming HTTP.</summary>
    public const int MaximumExpandStepCount = 16;

    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);
    private static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 220, 0);
    private static readonly RgbColor NotReadyRed = RgbColor.FromRgb(180, 30, 30);

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
