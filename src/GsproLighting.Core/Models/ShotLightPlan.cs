using GsproLighting.Core.Config;

namespace GsproLighting.Core.Models;

public sealed record ShotLightPlan(
    EffectSlot Slot,
    ShotDirection Direction,
    bool IsPutt)
{
    public RgbColor Color => Slot.Color;
}
