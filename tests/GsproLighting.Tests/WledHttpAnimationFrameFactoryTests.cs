using System.Text.Json;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpAnimationFrameFactoryTests
{
    [Fact]
    public void CreateRedBreathingCycle_UsesSolidRedAndConfiguredBrightnessCeiling()
    {
        var frames = WledHttpAnimationFrameFactory.CreateRedBreathingCycle(200);

        Assert.Equal([30, 64, 109, 155, 200, 155, 109, 64], frames.Select(ReadBrightness));
        Assert.All(frames, frame =>
        {
            var segment = ReadSegments(frame)[0];
            Assert.Equal(0, segment.GetProperty("fx").GetInt32());
            Assert.Equal([180, 30, 30], ReadPrimaryColor(segment));
            Assert.False(ReadRoot(frame).GetProperty("live").GetBoolean());
        });
    }

    [Fact]
    public void CreateReadySequence_IlluminatesSymmetricRangesThenHoldsFullGreen()
    {
        var frames = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount: 12, brightness: 180);

        Assert.Equal(7, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 1);
        AssertRange(firstSegments[1], id: 1, start: 1, stop: 11);
        AssertRange(firstSegments[2], id: 2, start: 11, stop: 12);

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 12);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(finalSegments[0]));
        Assert.Equal(0, finalSegments[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, finalSegments[2].GetProperty("stop").GetInt32());
        Assert.Equal(180, ReadBrightness(frames[^1]));
    }

    [Fact]
    public void CreateReadySequence_LimitsHttpRequestCountForLongStrips()
    {
        var frames = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount: 300, brightness: 255);

        Assert.Equal(WledHttpAnimationFrameFactory.MaximumReadyStepCount + 1, frames.Count);
    }

    private static JsonElement ReadRoot(WledHttpAnimationFrame frame) =>
        JsonSerializer.SerializeToElement(frame.Body);

    private static JsonElement[] ReadSegments(WledHttpAnimationFrame frame) =>
        ReadRoot(frame).GetProperty("seg").EnumerateArray().ToArray();

    private static int ReadBrightness(WledHttpAnimationFrame frame) =>
        ReadRoot(frame).GetProperty("bri").GetInt32();

    private static int[] ReadPrimaryColor(JsonElement segment) =>
        segment.GetProperty("col")[0].EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static void AssertRange(JsonElement segment, int id, int start, int stop)
    {
        Assert.Equal(id, segment.GetProperty("id").GetInt32());
        Assert.Equal(start, segment.GetProperty("start").GetInt32());
        Assert.Equal(stop, segment.GetProperty("stop").GetInt32());
        Assert.Equal(0, segment.GetProperty("fx").GetInt32());
    }
}
