using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ShotDirectionTests
{
    /// <summary>
    /// Default: |HLA| &lt;= 1.5 center; beyond → Left/Right by sign.
    /// </summary>
    [Theory]
    [InlineData(-5.0, ShotDirection.Left)]
    [InlineData(-1.6, ShotDirection.Left)]
    [InlineData(-1.5, ShotDirection.Center)]
    [InlineData(0.0, ShotDirection.Center)]
    [InlineData(1.5, ShotDirection.Center)]
    [InlineData(1.6, ShotDirection.Right)]
    [InlineData(8.0, ShotDirection.Right)]
    public void ClassifyDirection_UsesCenterThresholdOnly(
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

    [Theory]
    [InlineData(ShotDirection.Left, true, ShotDirection.Right)]
    [InlineData(ShotDirection.Center, true, ShotDirection.Center)]
    [InlineData(ShotDirection.Right, true, ShotDirection.Left)]
    [InlineData(ShotDirection.Left, false, ShotDirection.Left)]
    public void ApplyInvertLeftRight_SwapsSidesWhenEnabled(
        ShotDirection input,
        bool invert,
        ShotDirection expected)
    {
        var direction = ShotEffectMapper.ApplyInvertLeftRight(input, invert);

        Assert.Equal(expected, direction);
    }
}
