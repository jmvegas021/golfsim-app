using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Core.Services;

/// <summary>
/// Maps a live shot to an EffectConfig color (putt / pure / mishit / default pure).
/// </summary>
public sealed class ShotEffectMapper
{
    public RgbColor Map(ShotPayload shot, EffectConfig effects)
    {
        var speed = shot.BallData?.Speed;
        if (speed is double s && s > 0 && s <= effects.PuttMaxBallSpeedMph)
            return effects.Putt;

        var smash = shot.SmashFactor;
        if (smash is double smashValue)
        {
            if (smashValue >= effects.PureMinSmashFactor)
                return effects.PureStrike;
            if (smashValue <= effects.MishitMaxSmashFactor)
                return effects.Mishit;
        }

        var hla = shot.BallData?.Hla;
        if (hla is double axis && Math.Abs(axis) >= 8)
            return effects.Mishit;

        return effects.PureStrike;
    }
}
