using GsproLighting.Core.Config;
using Xunit;

namespace GsproLighting.Tests;

public sealed class EffectConfigSerializationTests
{
    [Fact]
    public void Load_LegacyRgbSlot_MigratesAndRetainsMissingDefaults()
    {
        using var directory = new TestDirectory();
        var configPath = directory.Write(
            "legacy.json",
            """
            {
              "Effects": {
                "Idle": { "R": 12, "G": 34, "B": 56 }
              }
            }
            """);

        var config = new ConfigStore(configPath).Load();

        AssertColor(config.Effects.Idle.Color, 12, 34, 56);
        Assert.Equal(EffectMode.Curated, config.Effects.Idle.Mode);
        Assert.Equal(EffectAnimations.Solid, config.Effects.Idle.Animation);
        AssertColor(config.Effects.NotReady.Color, 220, 20, 20);
        Assert.Equal(EffectAnimations.OutsideToCenter, config.Effects.NotReady.Animation);
        Assert.Equal(1.5, config.Effects.CenterHlaAbsDegrees);
    }

    [Fact]
    public void SaveAndLoad_NewConfig_RoundTripsEffectAndSettings()
    {
        using var directory = new TestDirectory();
        var configPath = directory.GetPath("appsettings.json");
        var expectedSlot = EffectSlot.WledPreset(RgbColor.FromRgb(7, 8, 9), 42);
        expectedSlot.Animation = EffectAnimations.Flash;
        var config = new AppConfig
        {
            Effects = new EffectConfig { Celebrate = expectedSlot },
            Wled = new WledConfig { InvertLeftRight = true },
            Logging = new LoggingConfig { ExportIncludeDays = 5 }
        };
        var store = new ConfigStore(configPath);

        store.Save(config);
        var loaded = store.Load();

        AssertColor(loaded.Effects.Celebrate.Color, 7, 8, 9);
        Assert.Equal(EffectMode.WledPreset, loaded.Effects.Celebrate.Mode);
        Assert.Equal(EffectAnimations.Flash, loaded.Effects.Celebrate.Animation);
        Assert.Equal(42, loaded.Effects.Celebrate.WledFxId);
        Assert.True(loaded.Wled.InvertLeftRight);
        Assert.Equal(5, loaded.Logging.ExportIncludeDays);
    }

    private static void AssertColor(RgbColor color, byte red, byte green, byte blue)
    {
        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
    }
}
