using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>Builds compact, deterministic Solid FX 0 frames for HTTP state animations.</summary>
public static class WledHttpAnimationFrameFactory
{
    public const int MaximumReadyStepCount = 8;

    private static readonly RgbColor Black = RgbColor.FromRgb(0, 0, 0);
    private static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 220, 0);
    private static readonly RgbColor NotReadyRed = RgbColor.FromRgb(180, 30, 30);
    private static readonly double[] BreathingLevels = [0.15, 0.32, 0.545, 0.775, 1, 0.775, 0.545, 0.32];
    private static readonly TimeSpan BreathingCadence = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan ReadyCadence = TimeSpan.FromMilliseconds(70);

    public static IReadOnlyList<WledHttpAnimationFrame> CreateRedBreathingCycle(byte brightness) =>
        BreathingLevels
            .Select(level => new WledHttpAnimationFrame(
                CreateSolidBody(NotReadyRed, ScaleBrightness(brightness, level)),
                BreathingCadence))
            .ToArray();

    public static IReadOnlyList<WledHttpAnimationFrame> CreateReadySequence(int ledCount, byte brightness)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var halfLength = (ledCount + 1) / 2;
        var stepCount = Math.Min(halfLength, MaximumReadyStepCount);
        var frames = new List<WledHttpAnimationFrame>(stepCount + 1);
        for (var step = 1; step <= stepCount; step++)
        {
            var litPerEdge = (int)Math.Ceiling((double)(step * halfLength) / stepCount);
            frames.Add(new WledHttpAnimationFrame(
                CreateReadyRangeBody(ledCount, litPerEdge, brightness),
                ReadyCadence));
        }

        frames.Add(new WledHttpAnimationFrame(
            CreateFinalReadyBody(ledCount, brightness),
            TimeSpan.Zero));
        return frames;
    }

    private static object CreateSolidBody(RgbColor color, byte brightness) =>
        new Dictionary<string, object?>
        {
            ["on"] = true,
            ["bri"] = brightness,
            ["live"] = false,
            ["seg"] = new[] { CreateColorSegment(0, color) }
        };

    private static object CreateReadyRangeBody(int ledCount, int litPerEdge, byte brightness)
    {
        var leftStop = Math.Min(litPerEdge, ledCount);
        var rightStart = Math.Max(leftStop, ledCount - litPerEdge);
        return CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, leftStop, ReadyGreen),
                CreateRangeSegment(1, leftStop, rightStart, Black),
                CreateRangeSegment(2, rightStart, ledCount, ReadyGreen)
            ]);
    }

    private static object CreateFinalReadyBody(int ledCount, byte brightness) =>
        CreateBody(
            brightness,
            [
                CreateRangeSegment(0, 0, ledCount, ReadyGreen),
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
