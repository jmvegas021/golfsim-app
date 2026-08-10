using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledCatalogValidatorTests
{
    [Fact]
    public void ValidateConfiguredIds_AllPresent_ReturnsNoWarnings()
    {
        var effects = new[]
        {
            Entry(EffectConfig.RippleFxId, "Ripple"),
            Entry(EffectConfig.ChaseFxId, "Chase"),
            Entry(EffectConfig.CelebrateFxId, "Fireworks"),
            Entry(EffectConfig.SparkleFxId, "Sparkle")
        };
        var palettes = new[]
        {
            Entry(EffectConfig.RedReefPaletteId, "Red Reef"),
            Entry(EffectConfig.AuroraPaletteId, "Aurora")
        };

        var warnings = WledCatalogValidator.ValidateConfiguredIds(effects, palettes);

        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateConfiguredIds_MissingFxId_ReturnsWarning()
    {
        var effects = new[] { Entry(1, "Solid") };
        var palettes = new[]
        {
            Entry(EffectConfig.RedReefPaletteId, "Red Reef"),
            Entry(EffectConfig.AuroraPaletteId, "Aurora")
        };

        var warnings = WledCatalogValidator.ValidateConfiguredIds(effects, palettes);

        Assert.Contains(warnings, w => w.Contains($"FX {EffectConfig.RippleFxId}"));
        Assert.Contains(warnings, w => w.Contains($"FX {EffectConfig.ChaseFxId}"));
        Assert.Contains(warnings, w => w.Contains($"FX {EffectConfig.CelebrateFxId}"));
        Assert.Contains(warnings, w => w.Contains($"FX {EffectConfig.SparkleFxId}"));
    }

    [Fact]
    public void ValidateConfiguredIds_MissingPaletteId_ReturnsWarning()
    {
        var effects = new[]
        {
            Entry(EffectConfig.RippleFxId, "Ripple"),
            Entry(EffectConfig.ChaseFxId, "Chase"),
            Entry(EffectConfig.CelebrateFxId, "Fireworks"),
            Entry(EffectConfig.SparkleFxId, "Sparkle")
        };
        var palettes = new[] { Entry(1, "Default") };

        var warnings = WledCatalogValidator.ValidateConfiguredIds(effects, palettes);

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains($"({EffectConfig.RedReefPaletteId})"));
        Assert.Contains(warnings, w => w.Contains($"({EffectConfig.AuroraPaletteId})"));
    }

    [Fact]
    public void ValidateConfiguredIds_EmptyCatalogs_ReturnsNoWarnings()
    {
        // Catalog not yet fetched (e.g. device unreachable) — don't false-positive.
        var warnings = WledCatalogValidator.ValidateConfiguredIds([], []);

        Assert.Empty(warnings);
    }

    private static WledNamedEntry Entry(int id, string name) => new() { Id = id, Name = name };
}
