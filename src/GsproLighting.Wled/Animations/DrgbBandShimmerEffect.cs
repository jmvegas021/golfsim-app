using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Hold-loop multi-layer color gradient confined to a concentrate band (or full strip).
/// Peak and wings stay in the state's hue — no white spike.
/// </summary>
public sealed class DrgbBandShimmerEffect : IDrgbHoldEffect
{
    /// <summary>Soft wing half-width as a fraction of band length.</summary>
    public const double WingHalfWidthFraction = 0.28;

    /// <summary>Bright core half-width as a fraction of band length.</summary>
    public const double CoreHalfWidthFraction = 0.10;

    /// <summary>Band-widths (or half-widths for center-out) traversed per second.</summary>
    public const double BandWidthsPerSecond = 2.4;

    /// <summary>Dim resting gain so the traveling gradient reads clearly.</summary>
    public const double BaseGain = 0.32;

    public const double WingGain = 0.72;

    public const double PeakGain = 1.0;

    private readonly LedBandRange _band;
    private readonly RgbColor _baseColor;
    private readonly DrgbShimmerMode _mode;

    public DrgbBandShimmerEffect(LedBandRange band, RgbColor baseColor, DrgbShimmerMode mode)
    {
        if (band.LitCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(band), "Band must light at least one LED.");
        _band = band;
        _baseColor = baseColor;
        _mode = mode;
    }

    public LedBandRange Band => _band;

    public RgbColor BaseColor => _baseColor;

    public DrgbShimmerMode Mode => _mode;

    public static DrgbBandShimmerEffect ForReady(int ledCount) =>
        new(
            DrgbConcentrateBandGeometry.ResolveCenter(ledCount),
            DrgbReadyFrameFactory.ReadyGreen,
            DrgbShimmerMode.CenterOut);

    public static DrgbBandShimmerEffect ForNotReady(int ledCount) =>
        ForFullStripCenterOut(ledCount, DrgbNotReadyFrameFactory.NotReadyRed);

    public static DrgbBandShimmerEffect ForWaiting(int ledCount) =>
        ForFullStripCenterOut(ledCount, DrgbWaitingFrameFactory.WaitingAqua);

    private static DrgbBandShimmerEffect ForFullStripCenterOut(int ledCount, RgbColor color)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));
        return new DrgbBandShimmerEffect(
            new LedBandRange(0, ledCount),
            color,
            DrgbShimmerMode.CenterOut);
    }

    public static DrgbBandShimmerEffect ForDirection(ShotDirection direction, int ledCount) =>
        new(
            DrgbConcentrateBandGeometry.Resolve(direction, ledCount),
            DrgbDirectionFrameFactory.ResolveColor(direction),
            ResolveMode(direction));

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
        var wing = Math.Max(1.5, lit * WingHalfWidthFraction);
        var core = Math.Max(1.0, lit * CoreHalfWidthFraction);

        for (var i = 0; i < lit; i++)
        {
            var dist = PeakDistance(i, lit, elapsed);
            var gain = ResolveGain(dist, core, wing);
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

    private static double DistanceToTravelingPeak(
        int localIndex,
        double from,
        double to,
        int lit,
        TimeSpan elapsed)
    {
        var span = Math.Abs(to - from);
        if (span < 0.5)
            return Math.Abs(localIndex - from);

        var phase = Wrap(elapsed.TotalSeconds * BandWidthsPerSecond, 1.0);
        var peak = from + ((to - from) * phase);
        return Math.Abs(localIndex - peak);
    }

    private static double DistanceToCenterOutFront(int localIndex, int lit, TimeSpan elapsed)
    {
        var mid = (lit - 1) / 2.0;
        var half = Math.Max(1.0, mid);
        var phase = Wrap(elapsed.TotalSeconds * BandWidthsPerSecond, 1.0);
        var front = phase * half;
        var distFromCenter = Math.Abs(localIndex - mid);
        return Math.Abs(distFromCenter - front);
    }

    private static double ResolveGain(double distance, double coreHalf, double wingHalf)
    {
        var core = CosineFalloff(distance, coreHalf);
        var wing = CosineFalloff(distance, wingHalf);
        // Map wing falloff into the BaseGain→WingGain band, then lift by PeakGain core.
        var wingRelative = (WingGain - BaseGain) / (PeakGain - BaseGain);
        var layered = Math.Max(core, wing * wingRelative);
        return BaseGain + ((PeakGain - BaseGain) * layered);
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
