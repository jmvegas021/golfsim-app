using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbStripAnimationPlayerTests
{
    [Fact]
    public async Task PlayAsync_SendsEachFrameInOrder()
    {
        var output = new RecordingWledOutput();
        var player = new DrgbStripAnimationPlayer(output);
        var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount: 6);

        await player.PlayAsync(frames, brightness: 200);

        Assert.Equal(frames.Count, output.PixelFrames.Count);
        Assert.Equal(frames[0].Pixels.ToArray(), output.PixelFrames[0].ToArray());
        Assert.Equal(frames[^1].Pixels.ToArray(), output.PixelFrames[^1].ToArray());
        Assert.All(output.Brightnesses, b => Assert.Equal((byte)200, b));
    }

    [Fact]
    public async Task HoldPixelsAsync_ResendsUntilCancelled()
    {
        var output = new RecordingWledOutput();
        var player = new DrgbStripAnimationPlayer(output);
        var pixels = DrgbReadyFrameFactory.CreateHoldPixels(8);
        using var cts = new CancellationTokenSource();

        var hold = player.HoldPixelsAsync(pixels, brightness: 180, duration: null, cts.Token);
        await WaitUntilAsync(() => output.PixelFrames.Count >= 2, TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hold);

        Assert.True(output.PixelFrames.Count >= 2);
        Assert.All(output.PixelFrames, frame => Assert.Equal(pixels, frame));
    }

    [Fact]
    public async Task HoldEffectAsync_StreamsChangingFramesUntilCancelled()
    {
        var output = new RecordingWledOutput();
        var player = new DrgbStripAnimationPlayer(output);
        var effect = DrgbBandShimmerEffect.ForReady(12);
        using var cts = new CancellationTokenSource();

        var hold = player.HoldEffectAsync(effect, ledCount: 12, brightness: 255, duration: null, cts.Token);
        await WaitUntilAsync(() => output.PixelFrames.Count >= 3, TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hold);

        Assert.True(output.PixelFrames.Count >= 3);
        Assert.False(output.PixelFrames[0].SequenceEqual(output.PixelFrames[^1]));
        Assert.All(output.Brightnesses, b => Assert.Equal(DrgbReadyFrameFactory.MaxIntensityBrightness, b));
        Assert.All(
            output.PixelFrames,
            frame => AssertOutsideBandBlack(frame, effect.Band));
    }

    [Fact]
    public async Task RunReadyThenNotReady_MorphsToRedShimmerHold_NoBreathe()
    {
        var output = new RecordingWledOutput();
        using var controller = new WledBallReadyDrgbController(output);
        using var readyCts = new CancellationTokenSource();

        var ready = controller.RunReadyAsync(12, 180, readyCts.Token);
        await WaitUntilAsync(
            () => controller.CurrentPose == WledBallReadyDrgbController.HeldPose.ReadyCenterBand,
            TimeSpan.FromSeconds(3));

        var notReady = controller.RunNotReadyAsync(12, 180);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
        await WaitUntilAsync(
            () => controller.CurrentPose == WledBallReadyDrgbController.HeldPose.NotReadyFull,
            TimeSpan.FromSeconds(3));

        await WaitUntilAsync(
            () =>
            {
                var frames = output.PixelFrames.ToArray();
                return frames.Length >= 2 &&
                       HasBandHue(frames[^1], new LedBandRange(0, 12), DrgbNotReadyFrameFactory.NotReadyRed) &&
                       !frames[^1].SequenceEqual(frames[^2]);
            },
            TimeSpan.FromSeconds(3));

        controller.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

        Assert.Equal(0, output.SolidCount);
        Assert.Contains(
            output.Brightnesses,
            b => b == DrgbReadyFrameFactory.MaxIntensityBrightness);
    }

    [Fact]
    public async Task RunDirection_HoldsYellowLeftBandShimmer_ThenReadySupersedes()
    {
        var output = new RecordingWledOutput();
        using var controller = new WledBallReadyDrgbController(output);
        var directions = new WledDirectionDrgbController(controller);
        var leftBand = DrgbConcentrateBandGeometry.ResolveLeft(12);

        var left = directions.RunDirectionAsync(ShotDirection.Left, 12, 180);
        await WaitUntilAsync(
            () => controller.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionLeft &&
                  output.PixelFrames.Any(frame =>
                      HasBandHue(frame, leftBand, DrgbDirectionFrameFactory.DirectionSideYellow) &&
                      IsOutsideBandBlack(frame, leftBand)),
            TimeSpan.FromSeconds(3));

        var ready = controller.RunReadyAsync(12, 180);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => left);
        await WaitUntilAsync(
            () => controller.CurrentPose == WledBallReadyDrgbController.HeldPose.ReadyCenterBand,
            TimeSpan.FromSeconds(3));

        controller.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);

        var readyBand = DrgbConcentrateBandGeometry.ResolveCenter(12);
        Assert.Contains(
            output.PixelFrames,
            frame => HasBandHue(frame, leftBand, DrgbDirectionFrameFactory.DirectionSideYellow));
        Assert.Contains(
            output.PixelFrames,
            frame => HasBandHue(frame, readyBand, DrgbReadyFrameFactory.ReadyGreen));
    }

    private static void AssertOutsideBandBlack(IReadOnlyList<RgbColor> frame, LedBandRange band) =>
        Assert.True(IsOutsideBandBlack(frame, band));

    private static bool IsOutsideBandBlack(IReadOnlyList<RgbColor> frame, LedBandRange band)
    {
        for (var i = 0; i < frame.Count; i++)
        {
            if (i >= band.Start && i < band.EndExclusive)
                continue;
            if (frame[i].R != 0 || frame[i].G != 0 || frame[i].B != 0)
                return false;
        }

        return true;
    }

    private static bool HasBandHue(
        IReadOnlyList<RgbColor> frame,
        LedBandRange band,
        RgbColor expected)
    {
        var lit = false;
        for (var i = band.Start; i < band.EndExclusive && i < frame.Count; i++)
        {
            var pixel = frame[i];
            if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
                continue;
            lit = true;
            if (expected.R == 0 && pixel.R != 0)
                return false;
            if (expected.G == 0 && pixel.G != 0)
                return false;
            if (expected.B == 0 && pixel.B != 0)
                return false;
            if (expected.R > 0 && pixel.R == 0)
                return false;
            if (expected.G > 0 && pixel.G == 0)
                return false;
        }

        return lit;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(20);
        }

        Assert.Fail("Timed out waiting for condition.");
    }

    private sealed class RecordingWledOutput : IWledOutput
    {
        public List<IReadOnlyList<RgbColor>> PixelFrames { get; } = [];
        public List<byte> Brightnesses { get; } = [];
        public int SolidCount { get; private set; }

        public void Configure(WledConfig config)
        {
        }

        public Task SendSolidAsync(
            RgbColor color,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            SolidCount++;
            return Task.CompletedTask;
        }

        public Task SendPixelsAsync(
            IReadOnlyList<RgbColor> pixels,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            PixelFrames.Add(pixels.ToArray());
            Brightnesses.Add(brightness ?? 255);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
