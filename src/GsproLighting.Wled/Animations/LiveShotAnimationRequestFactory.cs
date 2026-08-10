using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Animations;

public sealed class LiveShotAnimationRequestFactory
{
    public LedAnimationRequest Create(ShotLightPlan plan, WledConfig config)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);

        return new LedAnimationRequest
        {
            Animation = plan.Slot.Animation,
            Color = plan.Slot.Color,
            LedCount = config.LedCount,
            InvertLeftRight = config.InvertLeftRight,
            Direction = ToAnimationDirection(plan.Direction),
            Brightness = config.Brightness
        };
    }

    private static AnimationDirection ToAnimationDirection(ShotDirection direction) =>
        direction switch
        {
            ShotDirection.Left => AnimationDirection.Left,
            ShotDirection.Right => AnimationDirection.Right,
            _ => AnimationDirection.Center
        };
}
