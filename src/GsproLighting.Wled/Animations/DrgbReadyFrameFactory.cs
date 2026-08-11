using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Pixel-frame Ready choreography for DDP streaming: blank → sides-in → full flash →
/// retract to a centered top band; hold shimmer is owned by <see cref="DrgbBandShimmerEffect"/>.
/// </summary>
public static class DrgbReadyFrameFactory
{
    /// <summary>
    /// ~45–50 FPS — a smidge slower than the prior 16ms (~60 FPS) aggressive intros,
    /// still far from the old multi-second crawl.
    /// </summary>
    public const int FrameCadenceMilliseconds = 20;

    /// <summary>
    /// Target LEDs advanced per side each fill/retract frame on long strips (~585).
    /// Prefer <see cref="ResolveLitAdvance"/> so short strips stay readable in tests/previews.
    /// </summary>
    public const int LitAdvancePerFrame = 10;

    /// <summary>
    /// Fraction of the strip lit for the resting Ready hold (~25–30% centered).
    /// Shared with hit-direction concentrate bands.
    /// </summary>
    public const double ConcentrateLitFraction = DrgbConcentrateBandGeometry.ConcentrateLitFraction;

    /// <summary>Status Ready always streams at full DDP intensity.</summary>
    public const byte MaxIntensityBrightness = 255;

    public static readonly RgbColor ReadyGreen = RgbColor.FromRgb(0, 255, 0);

    /// <summary>Adaptive per-side advance — ~12 on 585 LEDs, floor 2 on short strips.</summary>
    public static int ResolveLitAdvance(int ledCount) =>
        Math.Clamp(Math.Max(2, ledCount / 48), 2, LitAdvancePerFrame);

    public static int ResolveLitAdvance(int ledCount, double introSpeedScale) =>
        Math.Max(1, (int)Math.Round(ResolveLitAdvance(ledCount) * Math.Max(0.25, introSpeedScale)));

    public static IReadOnlyList<LedAnimationFrame> CreateReadySequence(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        var cadence = TimeSpan.FromMilliseconds(FrameCadenceMilliseconds);
        var advance = ResolveLitAdvance(ledCount, p.IntroSpeedScale);
        var concentrate = DrgbConcentrateBandGeometry.ResolveLitCount(
            ledCount,
            p.ConcentrateLitFraction);
        var maxFromEdge = (ledCount + 1) / 2;
        var fillSteps = (maxFromEdge + advance - 1) / advance;
        var retractSteps = Math.Max(0, (ledCount - concentrate) / (2 * advance));
        var frames = new List<LedAnimationFrame>(1 + fillSteps + retractSteps + 1);

        frames.Add(new LedAnimationFrame(CreateEmpty(ledCount), cadence));

        for (var litEach = advance; litEach < maxFromEdge; litEach += advance)
            frames.Add(new LedAnimationFrame(CreateEdgesIn(ledCount, litEach, ReadyGreen), cadence));

        if (concentrate >= ledCount)
        {
            frames.Add(new LedAnimationFrame(
                CreateCenterBand(ledCount, ledCount, ReadyGreen),
                TimeSpan.Zero));
            return frames;
        }

        frames.Add(new LedAnimationFrame(CreateEdgesIn(ledCount, maxFromEdge, ReadyGreen), cadence));

        for (var litCount = ledCount - (2 * advance);
             litCount > concentrate;
             litCount -= 2 * advance)
        {
            frames.Add(new LedAnimationFrame(CreateCenterBand(ledCount, litCount, ReadyGreen), cadence));
        }

        frames.Add(new LedAnimationFrame(
            CreateCenterBand(ledCount, concentrate, ReadyGreen),
            TimeSpan.Zero));
        return frames;
    }

    public static RgbColor[] CreateHoldPixels(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        var band = DrgbConcentrateBandGeometry.ResolveCenter(ledCount, p.ConcentrateLitFraction);
        return CreateBand(ledCount, band.Start, band.LitCount, ReadyGreen);
    }

    public static int ResolveConcentrateLitCount(
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction) =>
        DrgbConcentrateBandGeometry.ResolveLitCount(ledCount, concentrateLitFraction);

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
