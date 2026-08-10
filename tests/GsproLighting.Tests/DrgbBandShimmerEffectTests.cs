using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbBandShimmerEffectTests
{
    [Fact]
    public void RenderFrame_KeepsPixelsOutsideBandBlack()
    {
        const int ledCount = 20;
        var effect = DrgbBandShimmerEffect.ForReady(ledCount);
        var band = effect.Band;

        var frame = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(80));

        for (var i = 0; i < ledCount; i++)
        {
            if (i >= band.Start && i < band.EndExclusive)
                continue;
            Assert.True(IsBlack(frame[i]), $"Pixel {i} outside band should be black.");
        }
    }

    [Fact]
    public void RenderFrame_IsNonStaticAcrossElapsed()
    {
        const int ledCount = 24;
        var effect = DrgbBandShimmerEffect.ForReady(ledCount);

        var a = effect.RenderFrame(ledCount, TimeSpan.Zero);
        var b = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(200));

        Assert.False(a.SequenceEqual(b));
    }

    [Theory]
    [InlineData(ShotDirection.Left)]
    [InlineData(ShotDirection.Center)]
    [InlineData(ShotDirection.Right)]
    public void RenderFrame_TintsBandWithDirectionColor(ShotDirection direction)
    {
        const int ledCount = 24;
        var effect = DrgbBandShimmerEffect.ForDirection(direction, ledCount);
        var expected = DrgbDirectionFrameFactory.ResolveColor(direction);
        var frame = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(40));

        AssertBandMatchesHue(frame, effect.Band, expected);
        Assert.Contains(frame, pixel => !IsBlack(pixel));
    }

    [Fact]
    public void ForNotReady_UsesFullStripBandInRed()
    {
        const int ledCount = 16;
        var effect = DrgbBandShimmerEffect.ForNotReady(ledCount);

        Assert.Equal(0, effect.Band.Start);
        Assert.Equal(ledCount, effect.Band.LitCount);
        Assert.Equal(DrgbNotReadyFrameFactory.NotReadyRed, effect.BaseColor);

        var frame = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(120));
        AssertBandMatchesHue(frame, effect.Band, DrgbNotReadyFrameFactory.NotReadyRed);
        Assert.DoesNotContain(frame, pixel => pixel.G > 0 || pixel.B > 0);
    }

    [Fact]
    public void ForReady_GeometryMatchesConcentrateBandAt585()
    {
        const int ledCount = 585;
        var effect = DrgbBandShimmerEffect.ForReady(ledCount);
        var expected = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);

        Assert.Equal(165, expected.LitCount);
        Assert.Equal(expected, effect.Band);
        Assert.Equal(210, effect.Band.Start);
        Assert.Equal(375, effect.Band.EndExclusive);

        var frame = effect.RenderFrame(ledCount, TimeSpan.FromMilliseconds(64));
        for (var i = 0; i < ledCount; i++)
        {
            if (i < expected.Start || i >= expected.EndExclusive)
                Assert.True(IsBlack(frame[i]));
        }

        AssertBandMatchesHue(frame, expected, DrgbReadyFrameFactory.ReadyGreen);
    }

    [Fact]
    public void ForDirection_LeftRight_AbutCenterAt585()
    {
        const int ledCount = 585;
        var center = DrgbBandShimmerEffect.ForDirection(ShotDirection.Center, ledCount).Band;
        var left = DrgbBandShimmerEffect.ForDirection(ShotDirection.Left, ledCount).Band;
        var right = DrgbBandShimmerEffect.ForDirection(ShotDirection.Right, ledCount).Band;

        Assert.Equal(center.Start, left.EndExclusive);
        Assert.Equal(center.EndExclusive, right.Start);
        Assert.Equal(165, left.LitCount);
        Assert.Equal(165, right.LitCount);
    }

    private static void AssertBandMatchesHue(
        IReadOnlyList<RgbColor> frame,
        LedBandRange band,
        RgbColor expected)
    {
        for (var i = band.Start; i < band.EndExclusive; i++)
        {
            var pixel = frame[i];
            if (IsBlack(pixel))
                continue;
            if (expected.R == 0)
                Assert.Equal(0, pixel.R);
            if (expected.G == 0)
                Assert.Equal(0, pixel.G);
            if (expected.B == 0)
                Assert.Equal(0, pixel.B);
            if (expected.R > 0)
                Assert.True(pixel.R > 0);
            if (expected.G > 0)
                Assert.True(pixel.G > 0);
        }
    }

    private static bool IsBlack(RgbColor pixel) =>
        pixel.R == 0 && pixel.G == 0 && pixel.B == 0;
}
