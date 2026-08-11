using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

/// <summary>
/// Shared concentrate-band geometry for Ready hold and hit-direction cues.
/// Same lit fraction (~25–30%); Left/Right abut the center Ready zone.
/// </summary>
public static class DrgbConcentrateBandGeometry
{
    /// <summary>
    /// Fraction of the strip lit for Ready hold and direction bands (~25–30% centered).
    /// </summary>
    public const double ConcentrateLitFraction = 0.28;

    public static int ResolveLitCount(
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var fraction = Math.Clamp(concentrateLitFraction, 0.15, 0.45);
        var raw = (int)Math.Round(ledCount * fraction);
        var hold = Math.Clamp(raw, ledCount <= 2 ? 1 : 2, ledCount);
        if (ledCount % 2 == 0 && hold % 2 == 1)
            hold = Math.Min(ledCount, hold + 1);
        else if (ledCount % 2 == 1 && hold % 2 == 0)
            hold = Math.Min(ledCount, hold + 1);
        return hold;
    }

    public static LedBandRange ResolveCenter(
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction)
    {
        var lit = ResolveLitCount(ledCount, concentrateLitFraction);
        return new LedBandRange((ledCount - lit) / 2, lit);
    }

    /// <summary>
    /// Same-width band immediately left of the Ready center zone, clamped to strip start.
    /// LitCount always equals the center band — never wider.
    /// </summary>
    public static LedBandRange ResolveLeft(
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction)
    {
        var center = ResolveCenter(ledCount, concentrateLitFraction);
        var start = Math.Max(0, center.Start - center.LitCount);
        return new LedBandRange(start, center.LitCount);
    }

    /// <summary>
    /// Same-width band immediately right of the Ready center zone, clamped to strip end.
    /// LitCount always equals the center band — never wider.
    /// </summary>
    public static LedBandRange ResolveRight(
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction)
    {
        var center = ResolveCenter(ledCount, concentrateLitFraction);
        var start = center.EndExclusive;
        if (start + center.LitCount > ledCount)
            start = Math.Max(0, ledCount - center.LitCount);
        return new LedBandRange(start, center.LitCount);
    }

    public static LedBandRange Resolve(
        ShotDirection direction,
        int ledCount,
        double concentrateLitFraction = ConcentrateLitFraction) =>
        direction switch
        {
            ShotDirection.Left => ResolveLeft(ledCount, concentrateLitFraction),
            ShotDirection.Right => ResolveRight(ledCount, concentrateLitFraction),
            _ => ResolveCenter(ledCount, concentrateLitFraction)
        };
}

/// <summary>Inclusive start + lit width for a concentrate band on the strip.</summary>
public readonly record struct LedBandRange(int Start, int LitCount)
{
    public int EndExclusive => Start + LitCount;
}
