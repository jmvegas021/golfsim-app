using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ShotDirectionTests
{
    /// <summary>
    /// Default bands: |HLA| &lt;= 1.5 center; &lt;= 4.0 mid; beyond far.
    /// </summary>
    [Theory]
    [InlineData(-5.0, ShotDirection.FarLeft)]
    [InlineData(-4.1, ShotDirection.FarLeft)]
    [InlineData(-4.0, ShotDirection.MidLeft)]
    [InlineData(-2.0, ShotDirection.MidLeft)]
    [InlineData(-1.5, ShotDirection.Center)]
    [InlineData(0.0, ShotDirection.Center)]
    [InlineData(1.5, ShotDirection.Center)]
    [InlineData(2.0, ShotDirection.MidRight)]
    [InlineData(4.0, ShotDirection.MidRight)]
    [InlineData(4.1, ShotDirection.FarRight)]
    [InlineData(8.0, ShotDirection.FarRight)]
    public void ClassifyDirection_UsesCenterAndMidBands(
        double hla,
        ShotDirection expected)
    {
        var direction = ShotEffectMapper.ClassifyDirection(hla, 1.5, 4.0);

        Assert.Equal(expected, direction);
    }

    [Fact]
    public void ClassifyDirection_NullHla_DefaultsToCenter()
    {
        var direction = ShotEffectMapper.ClassifyDirection(null, 1.5, 4.0);

        Assert.Equal(ShotDirection.Center, direction);
    }

    [Theory]
    [InlineData(ShotDirection.FarLeft, true, ShotDirection.FarRight)]
    [InlineData(ShotDirection.MidLeft, true, ShotDirection.MidRight)]
    [InlineData(ShotDirection.Center, true, ShotDirection.Center)]
    [InlineData(ShotDirection.MidRight, true, ShotDirection.MidLeft)]
    [InlineData(ShotDirection.FarRight, true, ShotDirection.FarLeft)]
    [InlineData(ShotDirection.FarLeft, false, ShotDirection.FarLeft)]
    public void ApplyInvertLeftRight_SwapsSidesWhenEnabled(
        ShotDirection input,
        bool invert,
        ShotDirection expected)
    {
        var direction = ShotEffectMapper.ApplyInvertLeftRight(input, invert);

        Assert.Equal(expected, direction);
    }

    [Fact]
    public void ClassifyDirection_ClampsMidThresholdBelowCenter()
    {
        // mid 1.0 with center 1.5 → mid treated as 1.5, so 2.0 is far.
        var direction = ShotEffectMapper.ClassifyDirection(2.0, 1.5, 1.0);

        Assert.Equal(ShotDirection.FarRight, direction);
    }
}
