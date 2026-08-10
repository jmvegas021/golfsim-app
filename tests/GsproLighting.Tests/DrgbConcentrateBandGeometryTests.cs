using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbConcentrateBandGeometryTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(60)]
    public void ResolveLitCount_MatchesReadyConcentrate(int ledCount)
    {
        Assert.Equal(
            DrgbReadyFrameFactory.ResolveConcentrateLitCount(ledCount),
            DrgbConcentrateBandGeometry.ResolveLitCount(ledCount));
        Assert.Equal(0.28, DrgbConcentrateBandGeometry.ConcentrateLitFraction);
    }

    [Fact]
    public void ResolveCenter_MatchesReadyHoldBand()
    {
        const int ledCount = 12;
        var band = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        var hold = DrgbReadyFrameFactory.CreateHoldPixels(ledCount);

        Assert.Equal((ledCount - band.LitCount) / 2, band.Start);
        for (var i = 0; i < ledCount; i++)
        {
            var expectedLit = i >= band.Start && i < band.EndExclusive;
            var isLit = hold[i].G == DrgbReadyFrameFactory.ReadyGreen.G && hold[i].R == 0;
            Assert.Equal(expectedLit, isLit);
        }
    }

    [Fact]
    public void ResolveLeft_AbutsLeftEdgeOfCenterZone()
    {
        const int ledCount = 12;
        var center = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        var left = DrgbConcentrateBandGeometry.ResolveLeft(ledCount);

        Assert.Equal(center.LitCount, left.LitCount);
        Assert.Equal(center.Start, left.EndExclusive);
        Assert.True(left.Start >= 0);
    }

    [Fact]
    public void ResolveRight_AbutsRightEdgeOfCenterZone()
    {
        const int ledCount = 12;
        var center = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        var right = DrgbConcentrateBandGeometry.ResolveRight(ledCount);

        Assert.Equal(center.LitCount, right.LitCount);
        Assert.Equal(center.EndExclusive, right.Start);
        Assert.True(right.EndExclusive <= ledCount);
    }

    [Fact]
    public void ResolveLeft_ClampsToStripStartOnShortStrip()
    {
        const int ledCount = 4;
        var left = DrgbConcentrateBandGeometry.ResolveLeft(ledCount);
        Assert.Equal(0, left.Start);
        Assert.Equal(DrgbConcentrateBandGeometry.ResolveLitCount(ledCount), left.LitCount);
    }

    [Fact]
    public void ResolveRight_ClampsToStripEndOnShortStrip()
    {
        const int ledCount = 4;
        var right = DrgbConcentrateBandGeometry.ResolveRight(ledCount);
        var lit = DrgbConcentrateBandGeometry.ResolveLitCount(ledCount);
        Assert.Equal(ledCount - lit, right.Start);
        Assert.Equal(lit, right.LitCount);
        Assert.Equal(ledCount, right.EndExclusive);
    }

    [Theory]
    [InlineData(ShotDirection.Left)]
    [InlineData(ShotDirection.Center)]
    [InlineData(ShotDirection.Right)]
    public void Resolve_MapsShotDirection(ShotDirection direction)
    {
        const int ledCount = 20;
        var band = DrgbConcentrateBandGeometry.Resolve(direction, ledCount);
        Assert.Equal(DrgbConcentrateBandGeometry.ResolveLitCount(ledCount), band.LitCount);
    }
}
