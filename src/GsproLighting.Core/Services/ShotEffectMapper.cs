using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Core.Services;

/// <summary>
/// Maps a live shot to its configured quality effect and HLA direction bucket.
/// </summary>
public sealed class ShotEffectMapper
{
    private const double HighHlaAbsDegrees = 8;
    private const double HighSideSpinAbsRpm = 1500;
    private const double MildSideSpinAbsRpm = 700;
    private const double LowCarryMishitYards = 80;

    public ShotLightPlan MapPlan(ShotPayload shot, EffectConfig effects)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(effects);

        var isPutt = IsPutt(shot, effects);
        var slot = isPutt ? effects.Putt : SelectStrikeSlot(shot, effects);
        var direction = ClassifyDirection(
            shot.BallData?.Hla,
            effects.CenterHlaAbsDegrees,
            effects.MidHlaAbsDegrees);
        return new ShotLightPlan(slot, direction, isPutt);
    }

    public RgbColor Map(ShotPayload shot, EffectConfig effects) =>
        MapPlan(shot, effects).Color;

    /// <summary>
    /// Buckets HLA into five hit-direction bands.
    /// Defaults: |HLA| &lt;= 1.5° center (green); &lt;= 4.0° mid yellow; beyond far red.
    /// Negative HLA → left; positive → right.
    /// </summary>
    public static ShotDirection ClassifyDirection(
        double? hla,
        double centerHlaAbsDegrees,
        double midHlaAbsDegrees = 4.0)
    {
        if (hla is not double angle || double.IsNaN(angle))
            return ShotDirection.Center;

        var centerThreshold = Math.Abs(centerHlaAbsDegrees);
        var midThreshold = Math.Max(Math.Abs(midHlaAbsDegrees), centerThreshold);
        var abs = Math.Abs(angle);

        if (abs <= centerThreshold)
            return ShotDirection.Center;

        var isLeft = angle < 0;
        if (abs <= midThreshold)
            return isLeft ? ShotDirection.MidLeft : ShotDirection.MidRight;

        return isLeft ? ShotDirection.FarLeft : ShotDirection.FarRight;
    }

    /// <summary>Swaps left/right buckets when the strip is mounted mirrored.</summary>
    public static ShotDirection ApplyInvertLeftRight(ShotDirection direction, bool invert)
    {
        if (!invert)
            return direction;

        return direction switch
        {
            ShotDirection.FarLeft => ShotDirection.FarRight,
            ShotDirection.MidLeft => ShotDirection.MidRight,
            ShotDirection.MidRight => ShotDirection.MidLeft,
            ShotDirection.FarRight => ShotDirection.FarLeft,
            _ => direction
        };
    }

    public static bool IsPutt(ShotPayload shot, EffectConfig effects)
    {
        if (shot.IsPutting == true)
            return true;

        if (shot.SpinType is string spinType &&
            (spinType.Contains("putt", StringComparison.OrdinalIgnoreCase) ||
             spinType.Contains("roll", StringComparison.OrdinalIgnoreCase)))
            return true;

        var speed = shot.BallData?.Speed;
        if (speed is double s && s > 0 && s <= effects.PuttMaxBallSpeedMph)
            return true;

        var carry = shot.BallData?.CarryDistance;
        var vla = shot.BallData?.Vla;
        return carry is double c && c <= 40 &&
               speed is null or <= 35 &&
               vla is null or <= 8;
    }

    private static EffectSlot SelectStrikeSlot(ShotPayload shot, EffectConfig effects)
    {
        if (shot.SmashFactor is double smash)
        {
            if (smash >= effects.PureMinSmashFactor)
                return effects.PureStrike;
            if (smash <= effects.MishitMaxSmashFactor)
                return effects.Mishit;
        }

        var hla = shot.BallData?.Hla;
        var sideSpin = shot.BallData?.SideSpin;
        var carry = shot.BallData?.CarryDistance;

        if (hla is double axis && Math.Abs(axis) >= HighHlaAbsDegrees)
            return effects.Mishit;

        if (sideSpin is double side && Math.Abs(side) >= HighSideSpinAbsRpm)
            return effects.Mishit;

        // Full-swing short carry with meaningful curve → mishit heuristic.
        if (carry is double c && c > 0 && c < LowCarryMishitYards &&
            ((hla is double mildHla && Math.Abs(mildHla) >= 4) ||
             (sideSpin is double mildSide && Math.Abs(mildSide) >= MildSideSpinAbsRpm)))
            return effects.Mishit;

        return effects.PureStrike;
    }
}
