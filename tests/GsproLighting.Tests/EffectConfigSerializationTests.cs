using GsproLighting.Core.Config;
using Xunit;

namespace GsproLighting.Tests;

public sealed class EffectConfigSerializationTests
{
    [Fact]
    public void Load_LegacyRgbSlot_NormalizesToProductDefaults_KeepsThresholds()
    {
        using var directory = new TestDirectory();
        var configPath = directory.Write(
            "legacy.json",
            """
            {
              "Effects": {
                "Idle": { "R": 12, "G": 34, "B": 56 },
                "PureMinSmashFactor": 1.7,
                "CenterHlaAbsDegrees": 2.25
              }
            }
            """);

        var config = new ConfigStore(configPath).Load();

        // Legacy RGB deserializes into the slot, then Load rewrites product lighting.
        AssertRippleAmbient(config.Effects.Idle, 71, 255, 153);
        AssertRedChase(config.Effects.NotReady, 255, 92, 68);
        AssertRippleAmbient(config.Effects.Waiting, 255, 192, 28);
        AssertColor(config.Effects.WaterHazard.Color, 0, 168, 200);
        AssertColor(config.Effects.OutOfBounds.Color, 255, 42, 42);
        Assert.Equal(1.7, config.Effects.PureMinSmashFactor);
        Assert.Equal(2.25, config.Effects.CenterHlaAbsDegrees);
    }

    [Fact]
    public void SaveAndLoad_RewritesLightingToProductDefaults_KeepsOtherSettings()
    {
        using var directory = new TestDirectory();
        var configPath = directory.GetPath("appsettings.json");
        var customCelebrate = EffectSlot.WledPreset(RgbColor.FromRgb(7, 8, 9), 42);
        customCelebrate.Animation = EffectAnimations.Flash;
        var config = new AppConfig
        {
            Effects = new EffectConfig
            {
                Celebrate = customCelebrate,
                PureMinSmashFactor = 1.55
            },
            Wled = new WledConfig { InvertLeftRight = true },
            Logging = new LoggingConfig { ExportIncludeDays = 5 }
        };
        var store = new ConfigStore(configPath);

        store.Save(config);
        var loaded = store.Load();

        AssertColor(loaded.Effects.Celebrate.Color, 255, 213, 74);
        Assert.Equal(EffectMode.WledPreset, loaded.Effects.Celebrate.Mode);
        Assert.Equal(EffectConfig.CelebrateFxId, loaded.Effects.Celebrate.WledFxId);
        Assert.Equal(1.55, loaded.Effects.PureMinSmashFactor);
        Assert.True(loaded.Wled.InvertLeftRight);
        Assert.Equal(5, loaded.Logging.ExportIncludeDays);
    }

    [Fact]
    public void Save_PersistsNormalizedProductLighting()
    {
        using var directory = new TestDirectory();
        var configPath = directory.GetPath("appsettings.json");
        var store = new ConfigStore(configPath);
        var config = new AppConfig
        {
            Effects = new EffectConfig
            {
                Idle = EffectSlot.Curated(RgbColor.FromRgb(1, 2, 3), EffectAnimations.Solid)
            }
        };

        store.Save(config);

        // In-memory config is normalized on Save; reload confirms disk match.
        AssertRippleAmbient(config.Effects.Idle, 71, 255, 153);
        var loaded = store.Load();
        AssertRippleAmbient(loaded.Effects.Idle, 71, 255, 153);
    }

    [Fact]
    public void ProductDefaults_MatchResearchedAuthoredPalette()
    {
        var effects = new EffectConfig();

        AssertRedChase(effects.NotReady, 255, 92, 68);
        AssertRippleAmbient(effects.Idle, 71, 255, 153);
        AssertRippleAmbient(effects.Waiting, 255, 192, 28);
        AssertColor(effects.PureStrike.Color, 0, 224, 90);
        Assert.Equal(EffectAnimations.DirectionAuto, effects.PureStrike.Animation);
        AssertColor(effects.Mishit.Color, 198, 40, 40);
        Assert.Equal(EffectAnimations.DirectionAuto, effects.Mishit.Animation);
        AssertColor(effects.Putt.Color, 79, 159, 224);
        Assert.Equal(EffectAnimations.DirectionAuto, effects.Putt.Animation);
        AssertColor(effects.Player.Color, 47, 160, 255);
        Assert.Equal(EffectAnimations.Pulse, effects.Player.Animation);
        AssertColor(effects.Celebrate.Color, 255, 213, 74);
        Assert.Equal(EffectMode.WledPreset, effects.Celebrate.Mode);
        Assert.Equal(EffectConfig.CelebrateFxId, effects.Celebrate.WledFxId);
        Assert.Equal(89, effects.Celebrate.WledFxId);
        Assert.Equal(EffectMode.WledPreset, effects.Hazard.Mode);
        Assert.Equal(EffectConfig.SparkleFxId, effects.Hazard.WledFxId);
        Assert.Equal(20, effects.Hazard.WledFxId);
        AssertColor(effects.WaterHazard.Color, 0, 168, 200);
        Assert.Equal(EffectAnimations.Flash, effects.WaterHazard.Animation);
        AssertColor(effects.OutOfBounds.Color, 255, 42, 42);
        Assert.Equal(EffectAnimations.Flash, effects.OutOfBounds.Animation);
    }

    [Fact]
    public void ResetLightingSlotsToProductDefaults_PreservesThresholds()
    {
        var effects = new EffectConfig
        {
            PureMinSmashFactor = 1.6,
            MishitMaxSmashFactor = 1.1,
            PuttMaxBallSpeedMph = 15,
            CenterHlaAbsDegrees = 2.0,
            MidHlaAbsDegrees = 5.5,
            Idle = EffectSlot.Curated(RgbColor.FromRgb(1, 2, 3), EffectAnimations.Solid)
        };

        effects.ResetLightingSlotsToProductDefaults();

        AssertRippleAmbient(effects.Idle, 71, 255, 153);
        Assert.Equal(1.6, effects.PureMinSmashFactor);
        Assert.Equal(1.1, effects.MishitMaxSmashFactor);
        Assert.Equal(15, effects.PuttMaxBallSpeedMph);
        Assert.Equal(2.0, effects.CenterHlaAbsDegrees);
        Assert.Equal(5.5, effects.MidHlaAbsDegrees);
    }

    private static void AssertRippleAmbient(EffectSlot slot, byte red, byte green, byte blue)
    {
        AssertColor(slot.Color, red, green, blue);
        Assert.Equal(EffectMode.WledPreset, slot.Mode);
        Assert.Equal(EffectConfig.RippleFxId, slot.WledFxId);
        Assert.NotNull(slot.WledOptions);
        Assert.Equal(EffectConfig.RippleTimingByte, slot.WledOptions!.Speed);
        Assert.Equal(EffectConfig.RippleTimingByte, slot.WledOptions.Intensity);
        Assert.Equal(EffectConfig.RedReefPaletteId, slot.WledOptions.PaletteId);
        Assert.True(slot.WledOptions.Overlay);
        Assert.Equal(79, slot.WledFxId);
        Assert.Equal(62, slot.WledOptions.PaletteId);
        Assert.Equal(38, slot.WledOptions.Speed);
    }

    private static void AssertRedChase(EffectSlot slot, byte red, byte green, byte blue)
    {
        AssertColor(slot.Color, red, green, blue);
        Assert.Equal(EffectMode.WledPreset, slot.Mode);
        Assert.Equal(EffectConfig.ChaseFxId, slot.WledFxId);
        Assert.NotNull(slot.WledOptions);
        Assert.Equal(EffectConfig.MaxTimingByte, slot.WledOptions!.Speed);
        Assert.Equal(EffectConfig.MaxTimingByte, slot.WledOptions.Intensity);
        Assert.Equal(EffectConfig.RedReefPaletteId, slot.WledOptions.PaletteId);
        Assert.NotEqual(EffectConfig.AuroraPaletteId, slot.WledOptions.PaletteId);
    }

    private static void AssertColor(RgbColor color, byte red, byte green, byte blue)
    {
        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
    }
}
