using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbReadyFrameFactoryTests
{
    [Fact]
    public void CreateReadySequence_FirstFrameIsDark_MidIsFull_FinalIsCenterBandHold()
    {
        const int ledCount = 12;
        var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
        var green = DrgbReadyFrameFactory.ReadyGreen;
        var advance = DrgbReadyFrameFactory.LitAdvancePerFrame;
        var concentrate = DrgbReadyFrameFactory.ResolveConcentrateLitCount(ledCount);

        Assert.True(frames.Count >= 3);
        Assert.All(frames[0].Pixels, pixel => Assert.True(IsBlack(pixel)));

        var firstLit = frames[1].Pixels;
        for (var i = 0; i < advance; i++)
        {
            Assert.True(SameRgb(green, firstLit[i]));
            Assert.True(SameRgb(green, firstLit[ledCount - 1 - i]));
        }

        Assert.All(
            firstLit.Skip(advance).Take(ledCount - (2 * advance)),
            pixel => Assert.True(IsBlack(pixel)));

        var fullFrame = frames.First(frame => frame.Pixels.All(pixel => SameRgb(pixel, green)));
        Assert.Equal(ledCount, fullFrame.Pixels.Count(pixel => SameRgb(pixel, green)));

        var final = frames[^1];
        Assert.Equal(TimeSpan.Zero, final.Duration);
        Assert.Equal(concentrate, final.Pixels.Count(pixel => SameRgb(pixel, green)));
        Assert.Equal(ledCount - concentrate, final.Pixels.Count(IsBlack));
        AssertCenterBand(final.Pixels, concentrate, green);
        Assert.Equal(0.28, DrgbReadyFrameFactory.ConcentrateLitFraction);
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);
    }

    [Fact]
    public void CreateReadySequence_AdvancesByLitAdvancePerFrame()
    {
        const int ledCount = 10;
        var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
        var green = DrgbReadyFrameFactory.ReadyGreen;
        var advance = DrgbReadyFrameFactory.LitAdvancePerFrame;

        var firstFill = frames[1].Pixels;
        Assert.True(SameRgb(green, firstFill[advance - 1]));
        Assert.True(SameRgb(green, firstFill[ledCount - advance]));
        Assert.True(IsBlack(firstFill[advance]));
        Assert.True(IsBlack(firstFill[ledCount - advance - 1]));

        Assert.Equal(
            TimeSpan.FromMilliseconds(DrgbReadyFrameFactory.FrameCadenceMilliseconds),
            frames[0].Duration);
        Assert.Equal(16, DrgbReadyFrameFactory.FrameCadenceMilliseconds);
    }

    [Fact]
    public void CreateHoldPixels_IsCenteredBandAtConcentrateFraction()
    {
        const int ledCount = 8;
        var hold = DrgbReadyFrameFactory.CreateHoldPixels(ledCount);
        var concentrate = DrgbReadyFrameFactory.ResolveConcentrateLitCount(ledCount);

        Assert.Equal(ledCount, hold.Length);
        Assert.Equal(2, concentrate);
        Assert.Equal(concentrate, hold.Count(pixel => SameRgb(DrgbReadyFrameFactory.ReadyGreen, pixel)));
        Assert.Equal(ledCount - concentrate, hold.Count(IsBlack));
        AssertCenterBand(hold, concentrate, DrgbReadyFrameFactory.ReadyGreen);
        Assert.True(IsBlack(hold[0]));
        Assert.True(IsBlack(hold[^1]));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(60)]
    public void ResolveConcentrateLitCount_IsRoughlyQuarterToThirdCentered(int ledCount)
    {
        var concentrate = DrgbReadyFrameFactory.ResolveConcentrateLitCount(ledCount);
        var fraction = concentrate / (double)ledCount;

        Assert.InRange(fraction, 0.20, 0.40);
        Assert.True(concentrate < ledCount);
        Assert.Equal(ledCount % 2, concentrate % 2);
    }

    private static void AssertCenterBand(
        IReadOnlyList<RgbColor> pixels,
        int litCount,
        RgbColor color)
    {
        var start = (pixels.Count - litCount) / 2;
        for (var i = 0; i < pixels.Count; i++)
        {
            if (i >= start && i < start + litCount)
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
