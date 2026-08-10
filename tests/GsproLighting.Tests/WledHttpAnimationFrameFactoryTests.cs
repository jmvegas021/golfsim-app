using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpAnimationFrameFactoryTests
{
    [Fact]
    public void CreateRedBreathingCycle_StartsAtFullBrightnessAndStaysBetweenTenAndOneHundredPercent()
    {
        var frames = WledHttpAnimationFrameFactory.CreateRedBreathingCycle(200, ledCount: 12);

        Assert.Equal(200, ReadBrightness(frames[0]));
        Assert.Equal(
            [200, 176, 144, 110, 80, 56, 36, 20, 36, 56, 80, 110, 144, 176],
            frames.Select(ReadBrightness));
        Assert.Equal(20, frames.Min(ReadBrightness));
        Assert.Equal(200, frames.Max(ReadBrightness));
        Assert.All(frames, frame =>
        {
            var segments = ReadSegments(frame);
            AssertRange(segments[0], id: 0, start: 0, stop: 12);
            Assert.Equal(0, segments[0].GetProperty("fx").GetInt32());
            Assert.Equal([180, 30, 30], ReadPrimaryColor(segments[0]));
            Assert.Equal(0, segments[1].GetProperty("stop").GetInt32());
            Assert.Equal(0, segments[2].GetProperty("stop").GetInt32());
            Assert.False(ReadRoot(frame).GetProperty("live").GetBoolean());
            Assert.True(ReadBrightness(frame) >= 20);
        });
    }

    [Fact]
    public void CreateColorTransitionSequence_InterpolatesOnFullStripWithoutZeroBrightnessFlash()
    {
        var from = RgbColor.FromRgb(0, 220, 0);
        var to = RgbColor.FromRgb(180, 30, 30);
        var frames = WledHttpAnimationFrameFactory.CreateColorTransitionSequence(
            from,
            fromBrightness: 180,
            to,
            toBrightness: 180,
            ledCount: 12);

        Assert.Equal(WledHttpAnimationFrameFactory.ColorTransitionStepCount, frames.Count);
        Assert.All(frames.Take(frames.Count - 1), frame =>
        {
            Assert.True(ReadBrightness(frame) >= 1);
            Assert.Equal(
                TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ColorTransitionCadenceMilliseconds),
                frame.Duration);
        });
        Assert.Equal(TimeSpan.Zero, frames[^1].Duration);
        Assert.True(ReadBrightness(frames[^1]) >= 1);

        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 12);
        Assert.Equal(0, firstSegments[1].GetProperty("stop").GetInt32());

        var firstColor = ReadPrimaryColor(firstSegments[0]);
        var lastColor = ReadPrimaryColor(ReadSegments(frames[^1])[0]);
        Assert.Equal([180, 30, 30], lastColor);
        Assert.Equal(180, ReadBrightness(frames[^1]));
        Assert.True(firstColor[1] > lastColor[1]);
        Assert.True(firstColor[0] < lastColor[0]);
        Assert.DoesNotContain(0, frames.Select(ReadBrightness));
    }

    [Fact]
    public void CreateColorTransitionSequence_ReadyFromRed_MovesTowardGreenOnFullStrip()
    {
        var frames = WledHttpAnimationFrameFactory.CreateColorTransitionSequence(
            RgbColor.FromRgb(180, 30, 30),
            fromBrightness: 90,
            RgbColor.FromRgb(0, 220, 0),
            toBrightness: 180,
            ledCount: 8);

        var first = ReadPrimaryColor(ReadSegments(frames[0])[0]);
        var lastSegments = ReadSegments(frames[^1]);
        AssertRange(lastSegments[0], id: 0, start: 0, stop: 8);
        var last = ReadPrimaryColor(lastSegments[0]);
        Assert.True(first[1] < last[1]);
        Assert.True(first[0] > last[0]);
        Assert.Equal([0, 220, 0], last);
        Assert.Equal(180, ReadBrightness(frames[^1]));
        Assert.True(ReadBrightness(frames[0]) > 90);
    }

    [Fact]
    public void CreateNotReadyExpandSequence_IlluminatesFromCenterOutwardThenHoldsFullRed()
    {
        const int ledCount = 12;
        var frames = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(
            ledCount,
            brightness: 180);
        var expandSteps = WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount);

        Assert.Equal(expandSteps + 1, frames.Count);
        Assert.Equal(6, expandSteps);

        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([180, 30, 30], ReadPrimaryColor(firstSegments[1]));

        // Fine growth: +1 LED per side each frame (lit width +2).
        var litWidths = frames.Take(expandSteps)
            .Select(frame =>
            {
                var segments = ReadSegments(frame);
                return segments[1].GetProperty("stop").GetInt32() -
                    segments[1].GetProperty("start").GetInt32();
            })
            .ToArray();
        Assert.Equal([2, 4, 6, 8, 10, 12], litWidths);

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 12);
        Assert.Equal([180, 30, 30], ReadPrimaryColor(finalSegments[0]));
        Assert.Equal(180, ReadBrightness(frames[^1]));
        Assert.Equal(
            TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds),
            frames[0].Duration);
    }

    [Fact]
    public void CreateNotReadyExpandSequence_FinalHoldIsFullStripSolidRed()
    {
        var frames = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(
            ledCount: 20,
            brightness: 200);
        var hold = frames[^1];
        var segments = ReadSegments(hold);

        AssertRange(segments[0], id: 0, start: 0, stop: 20);
        Assert.Equal([180, 30, 30], ReadPrimaryColor(segments[0]));
        Assert.Equal(0, segments[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, segments[2].GetProperty("stop").GetInt32());
        Assert.DoesNotContain(
            frames,
            frame => ReadSegments(frame).Any(seg =>
                seg.TryGetProperty("col", out var col) &&
                col[0].EnumerateArray().ElementAt(1).GetInt32() == 220));
    }

    [Fact]
    public void CreateReadySequence_EdgesInThenChasesToCenterHoldBand()
    {
        const int ledCount = 12;
        var frames = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, brightness: 180);
        var holdLit = WledHttpReadyAnimationBuilder.ResolveHoldLitCount(ledCount);
        var edgesIn = WledHttpReadyAnimationBuilder.ResolveEdgesInStepCount(ledCount);
        var chase = WledHttpReadyAnimationBuilder.ResolveChaseStepCount(ledCount);

        Assert.Equal(edgesIn + chase, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        // Edges-in: lit on both ends, dark in the middle.
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 1);
        AssertRange(firstSegments[1], id: 1, start: 1, stop: 11);
        AssertRange(firstSegments[2], id: 2, start: 11, stop: 12);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(firstSegments[0]));
        Assert.Equal([0, 0, 0], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        var finalStart = finalSegments[1].GetProperty("start").GetInt32();
        var finalStop = finalSegments[1].GetProperty("stop").GetInt32();
        Assert.Equal(holdLit, finalStop - finalStart);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(finalSegments[1]));
        Assert.True(holdLit < ledCount);
        Assert.Equal(TimeSpan.Zero, frames[^1].Duration);
    }

    [Fact]
    public void CreateReadyChaseFromFullSequence_ShrinksToCenterWithoutBlackFlash()
    {
        const int ledCount = 12;
        var frames = WledHttpReadyAnimationBuilder.CreateReadyChaseFromFullSequence(ledCount, 180);
        var holdLit = WledHttpReadyAnimationBuilder.ResolveHoldLitCount(ledCount);

        using var first = JsonDocument.Parse(JsonSerializer.Serialize(frames[0].Body));
        Assert.Equal(0, first.RootElement.GetProperty("seg")[0].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, first.RootElement.GetProperty("seg")[0].GetProperty("stop").GetInt32());
        Assert.All(frames, frame => Assert.True(ReadBrightness(frame) > 0));

        var finalSegments = ReadSegments(frames[^1]);
        Assert.Equal(holdLit, finalSegments[1].GetProperty("stop").GetInt32() -
            finalSegments[1].GetProperty("start").GetInt32());
    }

    [Fact]
    public void CreateReadySequence_LimitsHttpRequestCountForLongStrips()
    {
        var frames = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount: 300, brightness: 255);
        var edgesIn = WledHttpReadyAnimationBuilder.ResolveEdgesInStepCount(300);
        var chase = WledHttpReadyAnimationBuilder.ResolveChaseStepCount(300);

        Assert.Equal(WledHttpAnimationFrameFactory.MaximumExpandStepCount, edgesIn);
        Assert.Equal(edgesIn + chase, frames.Count);
        Assert.True(frames.Count <= WledHttpAnimationFrameFactory.MaximumExpandStepCount * 2);
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
    public void CreateHitDirectionSequence_Left_FillsFromCenterTowardLeftHalfOnly()
    {
        const int ledCount = 12;
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Left,
            ledCount,
            brightness: 180);
        var steps = WledHttpAnimationFrameFactory.ResolveUnilateralStepCount((ledCount + 1) / 2);

        Assert.Equal(steps + 1, frames.Count);
        Assert.All(
            frames.Take(steps),
            frame => Assert.Equal(
                TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds),
                frame.Duration));
        Assert.Equal(TimeSpan.Zero, frames[^1].Duration);

        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 5);
        AssertRange(firstSegments[1], id: 1, start: 5, stop: 6);
        AssertRange(firstSegments[2], id: 2, start: 6, stop: 12);
        Assert.Equal([220, 180, 0], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[0], id: 0, start: 0, stop: 0);
        AssertRange(finalSegments[1], id: 1, start: 0, stop: 6);
        AssertRange(finalSegments[2], id: 2, start: 6, stop: 12);
        Assert.Equal([220, 180, 0], ReadPrimaryColor(finalSegments[1]));
    }

    [Fact]
    public void CreateHitDirectionSequence_Right_FillsFromCenterTowardRightHalfOnly()
    {
        const int ledCount = 12;
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Right,
            ledCount,
            brightness: 180);
        var steps = WledHttpAnimationFrameFactory.ResolveUnilateralStepCount(ledCount / 2);

        Assert.Equal(steps + 1, frames.Count);
        var firstSegments = ReadSegments(frames[0]);
        AssertRange(firstSegments[0], id: 0, start: 0, stop: 6);
        AssertRange(firstSegments[1], id: 1, start: 6, stop: 7);
        AssertRange(firstSegments[2], id: 2, start: 7, stop: 12);
        Assert.Equal([220, 180, 0], ReadPrimaryColor(firstSegments[1]));

        var finalSegments = ReadSegments(frames[^1]);
        AssertRange(finalSegments[1], id: 1, start: 6, stop: 12);
        AssertRange(finalSegments[2], id: 2, start: 12, stop: 12);
    }

    [Fact]
    public void CreateHitDirectionSequences_MatchExpandStepBudgetAndCadence()
    {
        const int ledCount = 60;
        var left = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Left,
            ledCount,
            180);
        var right = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Right,
            ledCount,
            180);
        var center = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Center,
            ledCount,
            180);

        Assert.Equal(left.Count, right.Count);
        Assert.Equal(left.Count, center.Count);
        Assert.Equal(
            WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount) + 1,
            left.Count);
        Assert.All(
            new[] { left, right, center }.SelectMany(frames => frames.Take(frames.Count - 1)),
            frame => Assert.Equal(
                TimeSpan.FromMilliseconds(WledHttpAnimationFrameFactory.ExpandCadenceMilliseconds),
                frame.Duration));
    }

    [Fact]
    public void CreateHitDirectionSequence_Center_ExpandsOutwardGreen()
    {
        const int ledCount = 12;
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Center,
            ledCount,
            brightness: 180);
        var steps = WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount);

        Assert.Equal(steps + 1, frames.Count);
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
    public void CreateHitDirectionSequence_LimitsHttpRequestCountForLongStrips()
    {
        var frames = WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
            ShotDirection.Left,
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
