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

    public static int ResolveLitCount(int ledCount)
    {
        if (ledCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(ledCount));

        var raw = (int)Math.Round(ledCount * ConcentrateLitFraction);
        var hold = Math.Clamp(raw, ledCount <= 2 ? 1 : 2, ledCount);
        if (ledCount % 2 == 0 && hold % 2 == 1)
            hold = Math.Min(ledCount, hold + 1);
        else if (ledCount % 2 == 1 && hold % 2 == 0)
            hold = Math.Min(ledCount, hold + 1);
        return hold;
    }

    public static LedBandRange ResolveCenter(int ledCount)
    {
        var lit = ResolveLitCount(ledCount);
        return new LedBandRange((ledCount - lit) / 2, lit);
    }

    /// <summary>
    /// Same-width band immediately left of the Ready center zone, clamped to strip start.
    /// </summary>
    public static LedBandRange ResolveLeft(int ledCount)
    {
        var center = ResolveCenter(ledCount);
        var start = Math.Max(0, center.Start - center.LitCount);
        return new LedBandRange(start, center.LitCount);
    }

    /// <summary>
    /// Same-width band immediately right of the Ready center zone, clamped to strip end.
    /// </summary>
    public static LedBandRange ResolveRight(int ledCount)
    {
        var center = ResolveCenter(ledCount);
        var start = center.EndExclusive;
        if (start + center.LitCount > ledCount)
            start = Math.Max(0, ledCount - center.LitCount);
        return new LedBandRange(start, center.LitCount);
    }

    public static LedBandRange Resolve(ShotDirection direction, int ledCount) =>
        direction switch
        {
            ShotDirection.Left => ResolveLeft(ledCount),
            ShotDirection.Right => ResolveRight(ledCount),
            _ => ResolveCenter(ledCount)
        };
}

/// <summary>Inclusive start + lit width for a concentrate band on the strip.</summary>
public readonly record struct LedBandRange(int Start, int LitCount)
{
    public int EndExclusive => Start + LitCount;
}
