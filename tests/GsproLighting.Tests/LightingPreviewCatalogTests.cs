using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using Xunit;

namespace GsproLighting.Tests;

public sealed class LightingPreviewCatalogTests
{
    [Fact]
    public void Create_IncludesAllPrimaryStatesWithDefaults()
    {
        var items = new LightingPreviewCatalog().Create(new EffectConfig());
        var ids = items.Select(item => item.Id).ToArray();

        Assert.Contains(LightingPreviewIds.Waiting, ids);
        Assert.Contains(LightingPreviewIds.NotReady, ids);
        Assert.Contains(LightingPreviewIds.Ready, ids);
        Assert.Contains(LightingPreviewIds.Pure, ids);
        Assert.Contains(LightingPreviewIds.Mishit, ids);
        Assert.Contains(LightingPreviewIds.Putt, ids);
        Assert.Contains(LightingPreviewIds.Player, ids);
        Assert.Contains(LightingPreviewIds.Celebrate, ids);
        Assert.Contains(LightingPreviewIds.Hazard, ids);
        Assert.Contains(LightingPreviewIds.Water, ids);
        Assert.Contains(LightingPreviewIds.OutOfBounds, ids);
        Assert.Contains(LightingPreviewIds.TestSweep, ids);
    }

    [Fact]
    public void Create_PureAndMishitSupportDirection()
    {
        var items = new LightingPreviewCatalog().Create(new EffectConfig());
        Assert.True(items.Single(i => i.Id == LightingPreviewIds.Pure).SupportsDirection);
        Assert.True(items.Single(i => i.Id == LightingPreviewIds.Mishit).SupportsDirection);
        Assert.False(items.Single(i => i.Id == LightingPreviewIds.Ready).SupportsDirection);
    }

    [Fact]
    public void Create_DimHoldStatesUseReducedBrightnessFactor()
    {
        var items = new LightingPreviewCatalog().Create(new EffectConfig());
        Assert.True(items.Single(i => i.Id == LightingPreviewIds.Waiting).HoldBrightnessFactor < 1);
        Assert.True(items.Single(i => i.Id == LightingPreviewIds.NotReady).HoldBrightnessFactor < 1);
    }

    [Fact]
    public void Create_UsesEffectConfigColors()
    {
        var effects = new EffectConfig
        {
            Idle = EffectSlot.Curated(RgbColor.FromRgb(1, 2, 3), EffectAnimations.CenterToOutside)
        };
        var ready = new LightingPreviewCatalog().Create(effects)
            .Single(i => i.Id == LightingPreviewIds.Ready);
        Assert.Equal(1, ready.Slot.Color.R);
        Assert.Equal(2, ready.Slot.Color.G);
        Assert.Equal(3, ready.Slot.Color.B);
    }

    [Fact]
    public void Create_ReadyAndWaitingUseRippleAmbientDefaults()
    {
        var items = new LightingPreviewCatalog().Create(new EffectConfig());
        var ready = items.Single(i => i.Id == LightingPreviewIds.Ready);
        var waiting = items.Single(i => i.Id == LightingPreviewIds.Waiting);

        Assert.Equal(EffectMode.WledPreset, ready.Slot.Mode);
        Assert.Equal(EffectConfig.RippleFxId, ready.Slot.WledFxId);
        Assert.Equal(EffectConfig.RedReefPaletteId, ready.Slot.WledOptions?.PaletteId);
        Assert.False(ready.HoldAsSolid);

        Assert.Equal(EffectMode.WledPreset, waiting.Slot.Mode);
        Assert.Equal(EffectConfig.RippleFxId, waiting.Slot.WledFxId);
        Assert.False(waiting.HoldAsSolid);
    }
}
