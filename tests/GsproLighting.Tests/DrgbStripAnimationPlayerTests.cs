using GsproLighting.Core.Config;
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
    public async Task RunReadyThenNotReady_MorphsFromCenterBandPose()
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
        controller.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

        Assert.Contains(
            output.PixelFrames,
            frame => frame.Any(p => p.Equals(DrgbNotReadyFrameFactory.NotReadyRed)));
        Assert.True(output.SolidCount >= 1);
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
