using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Hold-loop traveling highlight confined to a concentrate band (or full strip).
/// Base pixels use the state color at reduced gain; the peak restores full intensity.
/// </summary>
public sealed class DrgbBandShimmerEffect : IDrgbHoldEffect
{
    /// <summary>Soft highlight half-width in LEDs (full width ≈ 2×).</summary>
    public const int HighlightHalfWidthLeds = 8;

    /// <summary>Band-widths traversed per second (~1.0 keeps motion readable on long strips).</summary>
    public const double BandWidthsPerSecond = 1.0;

    /// <summary>Steady band gain so the traveling peak can read as brighter, same hue.</summary>
    public const double BaseGain = 0.55;

    public const double PeakGain = 1.0;

    private readonly LedBandRange _band;
    private readonly RgbColor _baseColor;

    public DrgbBandShimmerEffect(LedBandRange band, RgbColor baseColor)
    {
        if (band.LitCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(band), "Band must light at least one LED.");
        _band = band;
        _baseColor = baseColor;
    }

    public LedBandRange Band => _band;

    public RgbColor BaseColor => _baseColor;

    public static DrgbBandShimmerEffect ForReady(int ledCount) =>
        new(DrgbConcentrateBandGeometry.ResolveCenter(ledCount), DrgbReadyFrameFactory.ReadyGreen);

    public static DrgbBandShimmerEffect ForNotReady(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        return new DrgbBandShimmerEffect(
            new LedBandRange(0, ledCount),
            DrgbNotReadyFrameFactory.NotReadyRed);
    }

    public static DrgbBandShimmerEffect ForDirection(ShotDirection direction, int ledCount) =>
        new(
            DrgbConcentrateBandGeometry.Resolve(direction, ledCount),
            DrgbDirectionFrameFactory.ResolveColor(direction));

    public RgbColor[] RenderFrame(int ledCount, TimeSpan elapsed)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        if (_band.EndExclusive > ledCount)
            throw new ArgumentOutOfRangeException(nameof(ledCount), "Band exceeds strip length.");

        var pixels = DrgbReadyFrameFactory.CreateEmpty(ledCount);
        var lit = _band.LitCount;
        var halfWidth = Math.Clamp(HighlightHalfWidthLeds, 1, Math.Max(1, lit / 2));
        var centerLocal = Wrap(
            elapsed.TotalSeconds * BandWidthsPerSecond * lit,
            lit);

        for (var i = 0; i < lit; i++)
        {
            var dist = CircularDistance(i, centerLocal, lit);
            var falloff = CosineFalloff(dist, halfWidth);
            var gain = BaseGain + ((PeakGain - BaseGain) * falloff);
            pixels[_band.Start + i] = AnimationPixels.Scale(_baseColor, gain);
        }

        return pixels;
    }

    private static double Wrap(double value, int period)
    {
        var wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private static double CircularDistance(double a, double b, int period)
    {
        var delta = Math.Abs(a - b);
        return Math.Min(delta, period - delta);
    }

    private static double CosineFalloff(double distance, int halfWidth)
    {
        if (distance >= halfWidth)
            return 0;
        var t = distance / halfWidth;
        return 0.5 * (1 + Math.Cos(Math.PI * t));
    }
}
