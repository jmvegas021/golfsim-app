using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbNotReadyFrameFactoryTests
{
    [Fact]
    public void CreateExpandFromDark_EndsOnSolidRedHold_NoBreathFrames()
    {
        const int ledCount = 12;
        var frames = DrgbNotReadyFrameFactory.CreateExpandFromDark(ledCount);
        var red = DrgbNotReadyFrameFactory.NotReadyRed;

        Assert.True(frames.Count >= 2);
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);

        var final = frames[^1];
        Assert.Equal(TimeSpan.Zero, final.Duration);
        Assert.Equal(ledCount, final.Pixels.Count(pixel => SameRgb(pixel, red)));
        Assert.All(final.Pixels, pixel => Assert.True(SameRgb(red, pixel)));

        Assert.All(
            frames.Where(frame => frame.Duration > TimeSpan.Zero),
            frame => Assert.Equal(
                TimeSpan.FromMilliseconds(DrgbNotReadyFrameFactory.FrameCadenceMilliseconds),
                frame.Duration));
    }

    [Fact]
    public void CreateFromReadyCenterBand_MorphsThenExpandsToFullSolidRed()
    {
        const int ledCount = 10;
        var frames = DrgbNotReadyFrameFactory.CreateFromReadyCenterBand(ledCount);
        var red = DrgbNotReadyFrameFactory.NotReadyRed;
        var green = DrgbReadyFrameFactory.ReadyGreen;
        var concentrate = DrgbReadyFrameFactory.ResolveConcentrateLitCount(ledCount);
        var center = ledCount / 2;

        Assert.True(frames.Count > DrgbNotReadyFrameFactory.MorphStepCount);
        Assert.Equal(concentrate, frames[0].Pixels.Count(pixel => !IsBlack(pixel)));
        Assert.True(IsBlack(frames[0].Pixels[0]));
        Assert.True(IsBlack(frames[0].Pixels[^1]));
        Assert.False(SameRgb(green, frames[0].Pixels[center]));
        Assert.True(frames[0].Pixels[center].R > 0);
        Assert.True(frames[0].Pixels[center].G < green.G);

        var final = frames[^1];
        Assert.Equal(TimeSpan.Zero, final.Duration);
        Assert.All(final.Pixels, pixel => Assert.True(SameRgb(red, pixel)));
    }

    [Fact]
    public void CreateHoldPixels_IsFullStripSolidRed()
    {
        const int ledCount = 8;
        var hold = DrgbNotReadyFrameFactory.CreateHoldPixels(ledCount);
        Assert.Equal(ledCount, hold.Length);
        Assert.All(hold, pixel => Assert.True(SameRgb(DrgbNotReadyFrameFactory.NotReadyRed, pixel)));
    }

    private static bool IsBlack(RgbColor pixel) =>
        pixel.R == 0 && pixel.G == 0 && pixel.B == 0;

    private static bool SameRgb(RgbColor a, RgbColor b) =>
        a.R == b.R && a.G == b.G && a.B == b.B;
}
