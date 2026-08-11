using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Hold-loop multi-layer same-hue gradient confined to a concentrate band (or full strip).
/// Peak, mid-wing, and soft halo stay in the state's hue — no white spike.
/// </summary>
public sealed class DrgbBandShimmerEffect : IDrgbHoldEffect
{
    /// <summary>Soft outer halo half-width as a fraction of band length.</summary>
    public const double HaloHalfWidthFraction = 0.42;

    /// <summary>Soft wing half-width as a fraction of band length.</summary>
    public const double WingHalfWidthFraction = 0.26;

    /// <summary>Bright core half-width as a fraction of band length.</summary>
    public const double CoreHalfWidthFraction = 0.09;

    /// <summary>
    /// Band-widths (or half-widths for center-out) traversed per second.
    /// ~0.9 reads as a slow breath / shimmer without feeling sluggish.
    /// </summary>
    public const double BandWidthsPerSecond = 0.9;

    /// <summary>Dim resting gain so the traveling gradient reads clearly.</summary>
    public const double BaseGain = 0.22;

    public const double HaloGain = 0.48;

    public const double WingGain = 0.78;

    public const double PeakGain = 1.0;

    private readonly LedBandRange _band;
    private readonly RgbColor _baseColor;
    private readonly DrgbShimmerMode _mode;
    private readonly DrgbStatusEffectParams _parameters;

    public DrgbBandShimmerEffect(
        LedBandRange band,
        RgbColor baseColor,
        DrgbShimmerMode mode,
        DrgbStatusEffectParams? parameters = null)
    {
        if (band.LitCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(band), "Band must light at least one LED.");
        _band = band;
        _baseColor = baseColor;
        _mode = mode;
        _parameters = parameters ?? DrgbStatusEffectParams.ProductDefaults;
    }

    public LedBandRange Band => _band;

    public RgbColor BaseColor => _baseColor;

    public DrgbShimmerMode Mode => _mode;

    public DrgbStatusEffectParams Parameters => _parameters;

    public static DrgbBandShimmerEffect ForReady(
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        return new(
            DrgbConcentrateBandGeometry.ResolveCenter(ledCount, p.ConcentrateLitFraction),
            DrgbReadyFrameFactory.ReadyGreen,
            DrgbShimmerMode.CenterOut,
            p);
    }

    public static DrgbBandShimmerEffect ForNotReady(
        int ledCount,
        DrgbStatusEffectParams? parameters = null) =>
        ForFullStripCenterOut(ledCount, DrgbNotReadyFrameFactory.NotReadyRed, parameters);

    public static DrgbBandShimmerEffect ForWaiting(
        int ledCount,
        DrgbStatusEffectParams? parameters = null) =>
        ForFullStripCenterOut(ledCount, DrgbWaitingFrameFactory.WaitingAqua, parameters);

    private static DrgbBandShimmerEffect ForFullStripCenterOut(
        int ledCount,
        RgbColor color,
        DrgbStatusEffectParams? parameters)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        return new DrgbBandShimmerEffect(
            new LedBandRange(0, ledCount),
            color,
            DrgbShimmerMode.CenterOut,
            parameters);
    }

    public static DrgbBandShimmerEffect ForDirection(
        ShotDirection direction,
        int ledCount,
        DrgbStatusEffectParams? parameters = null)
    {
        var p = parameters ?? DrgbStatusEffectParams.ProductDefaults;
        return new(
            DrgbConcentrateBandGeometry.Resolve(direction, ledCount, p.ConcentrateLitFraction),
            DrgbDirectionFrameFactory.ResolveColor(direction),
            ResolveMode(direction),
            p);
    }

    public static DrgbShimmerMode ResolveMode(ShotDirection direction) =>
        direction switch
        {
            ShotDirection.Left => DrgbShimmerMode.TowardLeft,
            ShotDirection.Right => DrgbShimmerMode.TowardRight,
            _ => DrgbShimmerMode.CenterOut
        };

    public RgbColor[] RenderFrame(int ledCount, TimeSpan elapsed)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        if (_band.EndExclusive > ledCount)
            throw new ArgumentOutOfRangeException(nameof(ledCount), "Band exceeds strip length.");

        var pixels = DrgbReadyFrameFactory.CreateEmpty(ledCount);
        var lit = _band.LitCount;
        var halo = Math.Max(2.0, lit * _parameters.HaloHalfWidthFraction);
        var wing = Math.Max(1.5, lit * _parameters.WingHalfWidthFraction);
        var core = Math.Max(1.0, lit * _parameters.CoreHalfWidthFraction);

        for (var i = 0; i < lit; i++)
        {
            var dist = PeakDistance(i, lit, elapsed);
            var gain = ResolveGain(dist, core, wing, halo);
            pixels[_band.Start + i] = AnimationPixels.Scale(_baseColor, gain);
        }

        return pixels;
    }

    private double PeakDistance(int localIndex, int lit, TimeSpan elapsed)
    {
        return _mode switch
        {
            DrgbShimmerMode.TowardLeft => DistanceToTravelingPeak(
                localIndex,
                from: lit - 1,
                to: 0,
                lit,
                elapsed),
            DrgbShimmerMode.TowardRight => DistanceToTravelingPeak(
                localIndex,
                from: 0,
                to: lit - 1,
                lit,
                elapsed),
            _ => DistanceToCenterOutFront(localIndex, lit, elapsed)
        };
    }

    private double DistanceToTravelingPeak(
        int localIndex,
        double from,
        double to,
        int lit,
        TimeSpan elapsed)
    {
        var span = Math.Abs(to - from);
        if (span < 0.5)
            return Math.Abs(localIndex - from);

        var phase = Wrap(elapsed.TotalSeconds * _parameters.ShimmerBandWidthsPerSecond, 1.0);
        var peak = from + ((to - from) * phase);
        return Math.Abs(localIndex - peak);
    }

    private double DistanceToCenterOutFront(int localIndex, int lit, TimeSpan elapsed)
    {
        var mid = (lit - 1) / 2.0;
        var half = Math.Max(1.0, mid);
        var phase = Wrap(elapsed.TotalSeconds * _parameters.ShimmerBandWidthsPerSecond, 1.0);
        var front = phase * half;
        var distFromCenter = Math.Abs(localIndex - mid);
        return Math.Abs(distFromCenter - front);
    }

    private double ResolveGain(double distance, double coreHalf, double wingHalf, double haloHalf)
    {
        var core = CosineFalloff(distance, coreHalf);
        var wing = CosineFalloff(distance, wingHalf);
        var halo = CosineFalloff(distance, haloHalf);
        var peak = _parameters.PeakGain;
        var baseGain = _parameters.BaseGain;
        var span = Math.Max(0.01, peak - baseGain);

        // Layer same-hue halo → wing → peak so the gradient reads richer without whitening.
        var layered = Math.Max(
            core,
            Math.Max(
                wing * ((WingGain - BaseGain) / (PeakGain - BaseGain)),
                halo * ((HaloGain - BaseGain) / (PeakGain - BaseGain))));
        return baseGain + (span * layered);
    }

    private static double Wrap(double value, double period)
    {
        if (period <= 0)
            return 0;
        var wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private static double CosineFalloff(double distance, double halfWidth)
    {
        if (distance >= halfWidth)
            return 0;
        var t = distance / halfWidth;
        return 0.5 * (1 + Math.Cos(Math.PI * t));
    }
}
