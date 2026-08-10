using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame Ready choreography for DRGB streaming: blank → sides-in → full →
/// retract to a solid top/center band (~<see cref="WledHttpReadyAnimationBuilder.ConcentrateLitFraction"/>).
/// </summary>
public static class DrgbReadyFrameFactory
{
    /// <summary>~42 FPS — within the 30–60 FPS DRGB target.</summary>
    public const int FrameCadenceMilliseconds = 24;

    public static readonly RgbColor ReadyGreen = WledHttpAnimationFrameFactory.ReadyGreen;

    public static IReadOnlyList<LedAnimationFrame> CreateReadySequence(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        var capacity = 1 + ((ledCount + 1) / 2) + Math.Max(0, (ledCount - concentrate) / 2) + 1;
        var frames = new List<LedAnimationFrame>(capacity);

        frames.Add(new LedAnimationFrame(CreateEmpty(ledCount), cadence));

        var maxFromEdge = (ledCount + 1) / 2;
        for (var litEach = 1; litEach <= maxFromEdge; litEach++)
            frames.Add(new LedAnimationFrame(CreateEdgesIn(ledCount, litEach, ReadyGreen), cadence));

        for (var litCount = ledCount - 2; litCount > concentrate; litCount -= 2)
            frames.Add(new LedAnimationFrame(CreateCenterBand(ledCount, litCount, ReadyGreen), cadence));

        frames.Add(new LedAnimationFrame(
            CreateCenterBand(ledCount, concentrate, ReadyGreen),
            TimeSpan.Zero));
        return frames;
    }

    public static RgbColor[] CreateHoldPixels(int ledCount) =>
        CreateCenterBand(
            ledCount,
            WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount),
            ReadyGreen);

    public static RgbColor[] CreateEmpty(int ledCount)
    {
        var pixels = new RgbColor[ledCount];
        Array.Fill(pixels, AnimationPixels.Black);
        return pixels;
    }

    public static RgbColor[] CreateEdgesIn(int ledCount, int litEachSide, RgbColor color)
    {
        var pixels = CreateEmpty(ledCount);
        var lit = Math.Clamp(litEachSide, 0, (ledCount + 1) / 2);
        for (var i = 0; i < lit; i++)
        {
            pixels[i] = color;
            pixels[ledCount - 1 - i] = color;
        }

        return pixels;
    }

    public static RgbColor[] CreateCenterBand(int ledCount, int litCount, RgbColor color)
    {
        var pixels = CreateEmpty(ledCount);
        var lit = Math.Clamp(litCount, 0, ledCount);
        var start = (ledCount - lit) / 2;
        for (var i = 0; i < lit; i++)
            pixels[start + i] = color;
        return pixels;
    }
}
