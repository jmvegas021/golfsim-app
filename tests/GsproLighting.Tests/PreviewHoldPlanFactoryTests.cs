using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class PreviewHoldPlanFactoryTests
{
    [Fact]
    public void Create_AppliesHoldBrightnessFactor()
    {
        var item = new LightingPreviewItem
        {
            Id = LightingPreviewIds.NotReady,
            Title = "Not ready",
            Description = "test",
            Slot = EffectSlot.Curated(RgbColor.FromRgb(220, 20, 20), EffectAnimations.OutsideToCenter),
            HoldBrightnessFactor = 0.33
        };
        var config = new WledConfig { Brightness = 180 };

        var plan = new PreviewHoldPlanFactory().Create(item, config);

        Assert.Equal((byte)Math.Round(180 * 0.33), plan.HoldBrightness);
        Assert.True(plan.HoldAsSolid);
    }

    [Fact]
    public void Create_RespectsDirectionOnlyWhenSupported()
    {
        var pure = new LightingPreviewItem
        {
            Id = LightingPreviewIds.Pure,
            Title = "Pure",
            Description = "test",
            Slot = EffectSlot.Curated(RgbColor.FromRgb(0, 220, 80), EffectAnimations.DirectionAuto),
            SupportsDirection = true,
            HoldAsSolid = false
        };
        var ready = new LightingPreviewItem
        {
            Id = LightingPreviewIds.Ready,
            Title = "Ready",
            Description = "test",
            Slot = EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.CenterToOutside),
            SupportsDirection = false
        };

        var factory = new PreviewHoldPlanFactory();
        var purePlan = factory.Create(pure, new WledConfig(), AnimationDirection.Left);
        var readyPlan = factory.Create(ready, new WledConfig(), AnimationDirection.Left);

        Assert.Equal(AnimationDirection.Left, purePlan.Direction);
        Assert.Equal(AnimationDirection.Center, readyPlan.Direction);
        Assert.False(purePlan.HoldAsSolid);
    }
}
