using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbReadyFrameFactoryTests
{
    [Fact]
    public void CreateReadySequence_FirstFrameIsDark_MidIsFull_FinalIsCenterBandOnly()
    {
        const int ledCount = 12;
        var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
        var green = DrgbReadyFrameFactory.ReadyGreen;
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);

        Assert.True(frames.Count >= 3);
        Assert.All(frames[0].Pixels, pixel => Assert.True(IsBlack(pixel)));

        var firstLit = frames[1].Pixels;
        Assert.Equal(green, firstLit[0]);
        Assert.Equal(green, firstLit[^1]);
        Assert.All(firstLit.Skip(1).Take(ledCount - 2), pixel => Assert.True(IsBlack(pixel)));

        var fullFrame = frames.First(frame => frame.Pixels.All(pixel => pixel.Equals(green)));
        Assert.Equal(ledCount, fullFrame.Pixels.Count(pixel => pixel.Equals(green)));

        var final = frames[^1];
        Assert.Equal(TimeSpan.Zero, final.Duration);
        Assert.Equal(concentrate, final.Pixels.Count(pixel => pixel.Equals(green)));
        Assert.Equal(ledCount - concentrate, final.Pixels.Count(IsBlack));
        var start = (ledCount - concentrate) / 2;
        for (var i = 0; i < concentrate; i++)
            Assert.Equal(green, final.Pixels[start + i]);
        Assert.True(IsBlack(final.Pixels[0]));
        Assert.True(IsBlack(final.Pixels[^1]));
    }

    [Fact]
    public void CreateReadySequence_AdvancesOneLedPerSidePerFrameWhenPossible()
    {
        const int ledCount = 10;
        var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
        var green = DrgbReadyFrameFactory.ReadyGreen;

        // blank + edges-in steps to full
        var edgeFrames = frames
            .Skip(1)
            .Take((ledCount + 1) / 2)
            .ToArray();
        for (var i = 0; i < edgeFrames.Length; i++)
        {
            var litEach = i + 1;
            Assert.Equal(green, edgeFrames[i].Pixels[litEach - 1]);
            Assert.Equal(green, edgeFrames[i].Pixels[ledCount - litEach]);
            if (litEach * 2 < ledCount)
            {
                Assert.True(IsBlack(edgeFrames[i].Pixels[litEach]));
                Assert.True(IsBlack(edgeFrames[i].Pixels[ledCount - litEach - 1]));
            }
        }

        Assert.Equal(
            TimeSpan.FromMilliseconds(DrgbReadyFrameFactory.FrameCadenceMilliseconds),
            frames[0].Duration);
    }

    [Fact]
    public void CreateHoldPixels_MatchesConcentrateBand()
    {
        const int ledCount = 8;
        var hold = DrgbReadyFrameFactory.CreateHoldPixels(ledCount);
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        Assert.Equal(concentrate, hold.Count(pixel => pixel.Equals(DrgbReadyFrameFactory.ReadyGreen)));
    }

    private static bool IsBlack(RgbColor pixel) =>
        pixel.R == 0 && pixel.G == 0 && pixel.B == 0;
}
