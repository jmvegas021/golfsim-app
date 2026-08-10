using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbDirectionFrameFactoryTests
{
    [Fact]
    public void CreateHoldPixels_Center_IsGreenReadyBand()
    {
        const int ledCount = 12;
        var hold = DrgbDirectionFrameFactory.CreateHoldPixels(ShotDirection.Center, ledCount);
        var ready = DrgbReadyFrameFactory.CreateHoldPixels(ledCount);

        Assert.Equal(ready, hold);
        Assert.Equal(DrgbReadyFrameFactory.ReadyGreen, DrgbDirectionFrameFactory.DirectionCenterGreen);
    }

    [Fact]
    public void CreateHoldPixels_Left_IsYellowAbuttingCenter()
    {
        const int ledCount = 12;
        var hold = DrgbDirectionFrameFactory.CreateHoldPixels(ShotDirection.Left, ledCount);
        var left = DrgbConcentrateBandGeometry.ResolveLeft(ledCount);
        var yellow = DrgbDirectionFrameFactory.DirectionSideYellow;

        AssertBand(hold, left, yellow);
        Assert.Equal(220, yellow.R);
        Assert.Equal(180, yellow.G);
        Assert.Equal(0, yellow.B);
    }

    [Fact]
    public void CreateHoldPixels_Right_IsYellowAbuttingCenter()
    {
        const int ledCount = 12;
        var hold = DrgbDirectionFrameFactory.CreateHoldPixels(ShotDirection.Right, ledCount);
        var right = DrgbConcentrateBandGeometry.ResolveRight(ledCount);

        AssertBand(hold, right, DrgbDirectionFrameFactory.DirectionSideYellow);
    }

    [Fact]
    public void CreateDirectionSequence_Left_SlidesFromCenterThenHolds()
    {
        const int ledCount = 12;
        var frames = DrgbDirectionFrameFactory.CreateDirectionSequence(ShotDirection.Left, ledCount);
        var target = DrgbConcentrateBandGeometry.ResolveLeft(ledCount);
        var yellow = DrgbDirectionFrameFactory.DirectionSideYellow;

        Assert.True(frames.Count >= 2);
        Assert.All(frames[0].Pixels, pixel => Assert.True(IsBlack(pixel)));
        Assert.Equal(TimeSpan.Zero, frames[^1].Duration);
        AssertBand(frames[^1].Pixels, target, yellow);

        var centerStart = DrgbConcentrateBandGeometry.ResolveCenter(ledCount).Start;
        Assert.Contains(
            frames.Skip(1).Take(frames.Count - 2),
            frame => frame.Pixels[centerStart].R == yellow.R);
    }

    [Fact]
    public void CreateDirectionSequence_Center_ExpandsGreenIntoReadyBand()
    {
        const int ledCount = 12;
        var frames = DrgbDirectionFrameFactory.CreateDirectionSequence(ShotDirection.Center, ledCount);
        var target = DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        var green = DrgbDirectionFrameFactory.DirectionCenterGreen;

        Assert.Equal(TimeSpan.Zero, frames[^1].Duration);
        AssertBand(frames[^1].Pixels, target, green);
        Assert.Equal(
            TimeSpan.FromMilliseconds(DrgbDirectionFrameFactory.FrameCadenceMilliseconds),
            frames[0].Duration);
    }

    [Fact]
    public void CreateDirectionSequence_Right_FinalMatchesHoldPixels()
    {
        const int ledCount = 20;
        var frames = DrgbDirectionFrameFactory.CreateDirectionSequence(ShotDirection.Right, ledCount);
        var hold = DrgbDirectionFrameFactory.CreateHoldPixels(ShotDirection.Right, ledCount);

        Assert.Equal(hold, frames[^1].Pixels.ToArray());
    }

    private static void AssertBand(
        IReadOnlyList<RgbColor> pixels,
        LedBandRange band,
        RgbColor color)
    {
        Assert.Equal(band.LitCount, pixels.Count(pixel => SameRgb(pixel, color)));
        for (var i = 0; i < pixels.Count; i++)
        {
            if (i >= band.Start && i < band.EndExclusive)
                Assert.True(SameRgb(color, pixels[i]));
            else
                Assert.True(IsBlack(pixels[i]));
        }
    }

    private static bool IsBlack(RgbColor pixel) =>
        pixel.R == 0 && pixel.G == 0 && pixel.B == 0;

    private static bool SameRgb(RgbColor a, RgbColor b) =>
        a.R == b.R && a.G == b.G && a.B == b.B;
}
