using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ShotDirectionTests
{
    [Theory]
    [InlineData(-2.0, ShotDirection.Left)]
    [InlineData(-1.5, ShotDirection.Center)]
    [InlineData(0.0, ShotDirection.Center)]
    [InlineData(1.5, ShotDirection.Center)]
    [InlineData(2.0, ShotDirection.Right)]
    public void ClassifyDirection_UsesConfiguredCenterBand(
        double hla,
        ShotDirection expected)
    {
        var direction = ShotEffectMapper.ClassifyDirection(hla, 1.5);

        Assert.Equal(expected, direction);
    }

    [Fact]
    public void ClassifyDirection_NullHla_DefaultsToCenter()
    {
        var direction = ShotEffectMapper.ClassifyDirection(null, 1.5);

        Assert.Equal(ShotDirection.Center, direction);
    }
}
