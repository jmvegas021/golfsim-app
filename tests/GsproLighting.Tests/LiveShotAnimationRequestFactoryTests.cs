using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class LiveShotAnimationRequestFactoryTests
{
    [Theory]
    [InlineData(EffectAnimations.Pulse)]
    [InlineData(EffectAnimations.Flash)]
    [InlineData(EffectAnimations.Sweep)]
    [InlineData(EffectAnimations.Solid)]
    public void Create_PreservesSelectedCuratedAnimation(string animation)
    {
        var slot = EffectSlot.Curated(RgbColor.FromRgb(10, 20, 30), animation);
        var plan = new ShotLightPlan(slot, ShotDirection.Center, false);

        var request = new LiveShotAnimationRequestFactory().Create(plan, new WledConfig());

        Assert.Equal(animation, request.Animation);
    }

    [Theory]
    [InlineData(ShotDirection.FarLeft, AnimationDirection.Left)]
    [InlineData(ShotDirection.MidLeft, AnimationDirection.Left)]
    [InlineData(ShotDirection.Center, AnimationDirection.Center)]
    [InlineData(ShotDirection.MidRight, AnimationDirection.Right)]
    [InlineData(ShotDirection.FarRight, AnimationDirection.Right)]
    public void Create_MapsDirectionForDirectionAuto(
        ShotDirection direction,
        AnimationDirection expected)
    {
        var slot = EffectSlot.Curated(
            RgbColor.FromRgb(10, 20, 30),
            EffectAnimations.DirectionAuto);
        var plan = new ShotLightPlan(slot, direction, false);

        var request = new LiveShotAnimationRequestFactory().Create(plan, new WledConfig());

        Assert.Equal(EffectAnimations.DirectionAuto, request.Animation);
        Assert.Equal(expected, request.Direction);
    }
}
