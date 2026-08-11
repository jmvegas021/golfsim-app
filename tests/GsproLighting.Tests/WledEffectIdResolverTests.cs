using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledEffectIdResolverTests
{
    [Fact]
    public void ResolveRippleFxId_UsesExactCatalogName()
    {
        var effects = new[]
        {
            new WledNamedEntry { Id = 0, Name = "Solid" },
            new WledNamedEntry { Id = 2, Name = "Ripple" },
            new WledNamedEntry { Id = 3, Name = "Rainbow" }
        };

        Assert.Equal(2, WledEffectIdResolver.ResolveRippleFxId(effects));
    }

    [Fact]
    public void ResolveRippleFxId_FallsBackToStockId()
    {
        Assert.Equal(EffectConfig.RippleFxId, WledEffectIdResolver.ResolveRippleFxId(null));
        Assert.Equal(EffectConfig.RippleFxId, WledEffectIdResolver.ResolveRippleFxId([]));
    }
}
