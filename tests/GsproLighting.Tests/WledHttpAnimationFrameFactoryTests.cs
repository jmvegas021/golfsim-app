using System.Text.Json;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpAnimationFrameFactoryTests
{
    [Fact]
    public void CreateRedBreathingCycle_UsesSolidRedBetweenTenAndOneHundredPercent()
    {
        var frames = WledHttpAnimationFrameFactory.CreateRedBreathingCycle(200);

        Assert.Equal(
            [20, 36, 56, 80, 110, 144, 176, 200, 176, 144, 110, 80, 56, 36],
            frames.Select(ReadBrightness));
        Assert.Equal(20, frames.Min(ReadBrightness));
        Assert.Equal(200, frames.Max(ReadBrightness));
        Assert.All(frames, frame =>
        {
            var segment = ReadSegments(frame)[0];
            Assert.Equal(0, segment.GetProperty("fx").GetInt32());
            Assert.Equal([180, 30, 30], ReadPrimaryColor(segment));
            Assert.False(ReadRoot(frame).GetProperty("live").GetBoolean());
        });
    }

    [Fact]
    public void CreateNotReadyExpandSequence_IlluminatesFromCenterOutwardThenHoldsFullRed()
    {
        var frames = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(
            ledCount: 12,
            brightness: 180);

        Assert.Equal(13, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([180, 30, 30], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 12);
        Assert.Equal([180, 30, 30], ReadPrimaryColor(finalSegments[0]));
        Assert.Equal(0, finalSegments[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, finalSegments[2].GetProperty("stop").GetInt32());
        Assert.Equal(180, ReadBrightness(frames[^1]));
    }

    [Fact]
    public void CreateReadySequence_IlluminatesFromCenterOutwardThenHoldsFullGreen()
    {
        var frames = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount: 12, brightness: 180);

        Assert.Equal(13, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(firstSegments[1]));

        var midSegments = ReadSegments(frames[5]);
        AssertRange(midSegments[0], id: 0, start: 0, stop: 3);
        AssertRange(midSegments[1], id: 1, start: 3, stop: 9);
        AssertRange(midSegments[2], id: 2, start: 9, stop: 12);

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

        Assert.Equal(WledHttpAnimationFrameFactory.MaximumExpandStepCount + 1, frames.Count);
    }

    [Fact]
    public void CreateNotReadyExpandSequence_LimitsHttpRequestCountForLongStrips()
    {
        var frames = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(
            ledCount: 300,
            brightness: 255);

        Assert.Equal(WledHttpAnimationFrameFactory.MaximumExpandStepCount + 1, frames.Count);
    }

    [Fact]
    public void CreateHitDirectionSequence_FarLeft_FillsFromCenterTowardLeftOnly()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.FarLeft,
            ledCount: 12,
            brightness: 180);

        Assert.Equal(7, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 6);
        AssertRange(firstSegments[2], id: 2, start: 6, stop: 12);
        Assert.Equal([220, 40, 40], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 0);
        AssertRange(finalSegments[1], id: 1, start: 0, stop: 6);
        AssertRange(finalSegments[2], id: 2, start: 6, stop: 12);
        Assert.Equal([220, 40, 40], ReadPrimaryColor(finalSegments[1]));
    }

    [Fact]
    public void CreateHitDirectionSequence_FarRight_FillsFromCenterTowardRightOnly()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.FarRight,
            ledCount: 12,
            brightness: 180);

        Assert.Equal(7, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 6);
        AssertRange(firstSegments[1], id: 1, start: 6, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([220, 40, 40], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[1], id: 1, start: 6, stop: 12);
        AssertRange(finalSegments[2], id: 2, start: 12, stop: 12);
    }

    [Fact]
    public void CreateHitDirectionSequence_Center_ExpandsOutwardGreen()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Center,
            ledCount: 12,
            brightness: 180);

        Assert.Equal(13, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 12);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(finalSegments[0]));
    }

    [Fact]
    public void CreateHitDirectionSequence_MidLeft_UsesYellow()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.MidLeft,
            ledCount: 12,
            brightness: 200);

        var firstSegments = ReadSegments(frames[0]);
        Assert.Equal([220, 180, 0], ReadPrimaryColor(firstSegments[1]));
    }

    [Fact]
    public void CreateHitDirectionSequence_LimitsHttpRequestCountForLongStrips()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.FarLeft,
            ledCount: 300,
            brightness: 255);

        Assert.Equal(WledHttpAnimationFrameFactory.MaximumExpandStepCount + 1, frames.Count);
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
