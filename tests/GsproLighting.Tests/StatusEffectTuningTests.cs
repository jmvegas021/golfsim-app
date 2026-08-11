using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class StatusEffectTuningTests
{
    [Fact]
    public void Defaults_MatchProductLookMultipliers()
    {
        var tuning = new StatusEffectTuning();

        Assert.True(tuning.Ready.IsProductDefault());
        Assert.True(tuning.NotReady.IsProductDefault());
        Assert.True(tuning.Direction.IsProductDefault());
        Assert.True(tuning.Waiting.IsProductDefault());
        Assert.Equal(0.28, DrgbStatusEffectParams.FromTuning(tuning.Ready).ConcentrateLitFraction);
        Assert.Equal(
            DrgbBandShimmerEffect.BandWidthsPerSecond,
            DrgbStatusEffectParams.FromTuning(tuning.Ready).ShimmerBandWidthsPerSecond);
    }

    [Fact]
    public void FromTuning_ScalesShimmerSpeedAndIntensity()
    {
        var tuning = new StatusEffectStateTuning
        {
            Speed = 2.0,
            Intensity = 0.5,
            Layers = 1.0,
            BandSizePercent = 28
        };

        var parameters = DrgbStatusEffectParams.FromTuning(tuning);

        Assert.Equal(
            DrgbBandShimmerEffect.BandWidthsPerSecond * 2.0,
            parameters.ShimmerBandWidthsPerSecond);
        Assert.Equal(2.0, parameters.IntroSpeedScale);
        Assert.Equal(0.5, parameters.PeakGain);
        Assert.True(parameters.BaseGain < DrgbBandShimmerEffect.BaseGain);
    }

    [Fact]
    public void FromTuning_HonorsBandSizeAndLayers()
    {
        var tuning = new StatusEffectStateTuning
        {
            Speed = 1.0,
            Intensity = 1.0,
            Layers = 2.0,
            BandSizePercent = 40
        };

        var parameters = DrgbStatusEffectParams.FromTuning(tuning);

        Assert.Equal(0.40, parameters.ConcentrateLitFraction);
        Assert.True(parameters.WingHalfWidthFraction > DrgbBandShimmerEffect.WingHalfWidthFraction);
        Assert.True(parameters.HaloHalfWidthFraction > DrgbBandShimmerEffect.HaloHalfWidthFraction);
    }

    [Fact]
    public void ForReady_UsesTunedBandFractionAndSpeed()
    {
        var parameters = DrgbStatusEffectParams.FromTuning(new StatusEffectStateTuning
        {
            Speed = 2.0,
            BandSizePercent = 40
        });
        const int ledCount = 100;
        var effect = DrgbBandShimmerEffect.ForReady(ledCount, parameters);

        Assert.Equal(
            DrgbConcentrateBandGeometry.ResolveLitCount(ledCount, 0.40),
            effect.Band.LitCount);
        Assert.Equal(parameters.ShimmerBandWidthsPerSecond, effect.Parameters.ShimmerBandWidthsPerSecond);

        var slow = DrgbBandShimmerEffect.ForReady(
            ledCount,
            DrgbStatusEffectParams.FromTuning(new StatusEffectStateTuning { Speed = 0.5 }));
        var a = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(200));
        var b = slow.RenderFrame(ledCount, TimeSpan.FromMilliseconds(200));
        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public void CreateReadySequence_FasterSpeedProducesFewerFrames()
    {
        const int ledCount = 120;
        var slow = DrgbReadyFrameFactory.CreateReadySequence(
            ledCount,
            DrgbStatusEffectParams.FromTuning(new StatusEffectStateTuning { Speed = 0.5 }));
        var fast = DrgbReadyFrameFactory.CreateReadySequence(
            ledCount,
            DrgbStatusEffectParams.FromTuning(new StatusEffectStateTuning { Speed = 2.0 }));

        Assert.True(fast.Count < slow.Count);
    }

    [Fact]
    public void ResolveWaitingRipple_MapsSpeedIntensityLayers()
    {
        var tuning = new StatusEffectStateTuning
        {
            Speed = 2.0,
            Intensity = 0.5,
            Layers = 1.5
        };

        var (speed, intensity, brightness) =
            DrgbStatusEffectParams.ResolveWaitingRipple(tuning, brightness: 100);

        Assert.Equal(EffectConfig.RippleTimingByte * 2, speed);
        Assert.Equal(EffectConfig.RippleTimingByte / 2, intensity);
        Assert.Equal(150, brightness);
    }

    [Fact]
    public void ConfigStore_PersistsStatusTuning_KeepsDefaultsOtherwise()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("status-tuning.json");
        var store = new ConfigStore(path);
        var config = new AppConfig
        {
            Effects = new EffectConfig
            {
                StatusTuning = new StatusEffectTuning
                {
                    Ready =
                    {
                        Speed = 1.5,
                        Intensity = 0.8,
                        Layers = 1.2,
                        BandSizePercent = 35
                    }
                }
            }
        };

        store.Save(config);
        var loaded = store.Load();

        Assert.Equal(1.5, loaded.Effects.StatusTuning.Ready.Speed);
        Assert.Equal(0.8, loaded.Effects.StatusTuning.Ready.Intensity);
        Assert.Equal(1.2, loaded.Effects.StatusTuning.Ready.Layers);
        Assert.Equal(35, loaded.Effects.StatusTuning.Ready.BandSizePercent);
        Assert.True(loaded.Effects.StatusTuning.NotReady.IsProductDefault());
    }

    [Fact]
    public void ClampAll_BoundsMultipliersAndBandSize()
    {
        var tuning = new StatusEffectTuning
        {
            Ready =
            {
                Speed = 99,
                Intensity = 0.01,
                Layers = -5,
                BandSizePercent = 90
            }
        };

        tuning.ClampAll();

        Assert.Equal(StatusEffectStateTuning.MaxMultiplier, tuning.Ready.Speed);
        Assert.Equal(StatusEffectStateTuning.MinMultiplier, tuning.Ready.Intensity);
        Assert.Equal(StatusEffectStateTuning.MinMultiplier, tuning.Ready.Layers);
        Assert.Equal(StatusEffectStateTuning.MaxBandSizePercent, tuning.Ready.BandSizePercent);
    }

    [Fact]
    public void EffectConfigClone_CopiesStatusTuning()
    {
        var effects = new EffectConfig();
        effects.StatusTuning.Direction.Speed = 1.75;

        var clone = effects.Clone();
        clone.StatusTuning.Direction.Speed = 0.5;

        Assert.Equal(1.75, effects.StatusTuning.Direction.Speed);
        Assert.Equal(0.5, clone.StatusTuning.Direction.Speed);
    }
}
