using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame Ready choreography for DDP streaming: blank → sides-in → full flash →
/// retract to a centered top band; hold shimmer is owned by <see cref="DrgbBandShimmerEffect"/>.
/// </summary>
public static class DrgbReadyFrameFactory
{
    /// <summary>~60 FPS — top of the 30–60 FPS DDP target.</summary>
    public const int FrameCadenceMilliseconds = 16;

    /// <summary>
    /// LEDs advanced per side each fill frame.
    /// TODO(P2): bump for ~585 LED strips — at 2/side/frame intro is ~4s before hold.
    /// </summary>
    public const int LitAdvancePerFrame = 2;

    /// <summary>
    /// Fraction of the strip lit for the resting Ready hold (~25–30% centered).
    /// Shared with hit-direction concentrate bands.
    /// </summary>
    public const double ConcentrateLitFraction = DrgbConcentrateBandGeometry.ConcentrateLitFraction;

    /// <summary>Status Ready always streams at full DDP intensity.</summary>
    public const byte MaxIntensityBrightness = 255;

    public static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 255, 0);

    public static IReadOnlyList<LedAnimationFrame> CreateReadySequence(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var concentrate = DrgbConcentrateBandGeometry.ResolveLitCount(ledCount);
        var maxFromEdge = (ledCount + 1) / 2;
        var fillSteps = (maxFromEdge + LitAdvancePerFrame - 1) / LitAdvancePerFrame;
        var retractSteps = Math.Max(0, (ledCount - concentrate) / (2 * LitAdvancePerFrame));
        var frames = new List<LedAnimationFrame>(1 + fillSteps + retractSteps + 1);

        frames.Add(new LedAnimationFrame(CreateEmpty(ledCount), cadence));

        for (var litEach = LitAdvancePerFrame; litEach < maxFromEdge; litEach += LitAdvancePerFrame)
            frames.Add(new LedAnimationFrame(CreateEdgesIn(ledCount, litEach, ReadyGreen), cadence));

        if (concentrate >= ledCount)
        {
            frames.Add(new LedAnimationFrame(
                CreateCenterBand(ledCount, ledCount, ReadyGreen),
                TimeSpan.Zero));
            return frames;
        }

        frames.Add(new LedAnimationFrame(CreateEdgesIn(ledCount, maxFromEdge, ReadyGreen), cadence));

        for (var litCount = ledCount - (2 * LitAdvancePerFrame);
             litCount > concentrate;
             litCount -= 2 * LitAdvancePerFrame)
        {
            frames.Add(new LedAnimationFrame(CreateCenterBand(ledCount, litCount, ReadyGreen), cadence));
        }

        frames.Add(new LedAnimationFrame(
            CreateCenterBand(ledCount, concentrate, ReadyGreen),
            TimeSpan.Zero));
        return frames;
    }

    public static RgbColor[] CreateHoldPixels(int ledCount)
    {
        var band = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        return CreateBand(ledCount, band.Start, band.LitCount, ReadyGreen);
    }

    public static int ResolveConcentrateLitCount(int ledCount) =>
        DrgbConcentrateBandGeometry.ResolveLitCount(ledCount);

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
        var lit = Math.Clamp(litCount, 0, ledCount);
        return CreateBand(ledCount, (ledCount - lit) / 2, lit, color);
    }

    public static RgbColor[] CreateBand(int ledCount, int start, int litCount, RgbColor color)
    {
        var pixels = CreateEmpty(ledCount);
        var lit = Math.Clamp(litCount, 0, ledCount);
        if (lit == 0)
            return pixels;

        var clampedStart = Math.Clamp(start, 0, ledCount - lit);
        for (var i = 0; i < lit; i++)
            pixels[clampedStart + i] = color;
        return pixels;
    }
}
