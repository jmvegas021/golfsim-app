using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;
using GsproLighting.Wled.Contracts;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

/// <summary>
/// After Direction min-hold (default 4s), synthesize Not Ready when GSPro never
/// sends status. Real Ready / Not Ready queued during the hold replace the fallback.
/// </summary>
public sealed class WledShotEffectSinkNotReadyFallbackTests
{
    [Fact]
    public async Task OnShotAsync_FallsBackToNotReady_WhenMinHoldElapsesWithNoStatus()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(60));
        using var readyDrgb = new WledBallReadyDrgbController(output, gate);
        using var sink = CreateSink(output, animationManager, readyDrgb);

        var shot = sink.OnShotAsync(SampleShot());
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionCenter,
            TimeSpan.FromSeconds(3));

        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.NotReadyFull,
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;

        Assert.Equal(0, handler.PostCount);
        Assert.Equal(WledBallReadyDrgbController.HeldPose.None, readyDrgb.CurrentPose);
    }

    [Fact]
    public async Task OnShotAsync_NotReadyFallback_CancelledWhenReadyArrivesDuringHold()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(80));
        using var readyDrgb = new WledBallReadyDrgbController(output, gate);
        using var sink = CreateSink(output, animationManager, readyDrgb);

        var shot = sink.OnShotAsync(SampleShot());
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionCenter,
            TimeSpan.FromSeconds(3));

        var ready = sink.OnBallReadyAsync(new ShotPayload());
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.ReadyCenterBand,
            TimeSpan.FromSeconds(3));

        // Fallback must not overwrite Ready after the min-hold window.
        await Task.Delay(120);
        Assert.Equal(WledBallReadyDrgbController.HeldPose.ReadyCenterBand, readyDrgb.CurrentPose);

        sink.CancelActiveEffects();
        await shot;
        await ready;
        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnShotAsync_NotReadyFallback_CancelledWhenRealNotReadyArrivesDuringHold()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(80));
        using var readyDrgb = new WledBallReadyDrgbController(output, gate);
        using var sink = CreateSink(output, animationManager, readyDrgb);

        var shot = sink.OnShotAsync(SampleShot(hla: -6));
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionLeft,
            TimeSpan.FromSeconds(3));

        var notReady = sink.OnBallNotReadyAsync();
        Assert.Equal(WledBallReadyDrgbController.HeldPose.DirectionLeft, readyDrgb.CurrentPose);

        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.NotReadyFull,
            TimeSpan.FromSeconds(3));

        await Task.Delay(120);
        Assert.Equal(WledBallReadyDrgbController.HeldPose.NotReadyFull, readyDrgb.CurrentPose);

        sink.CancelActiveEffects();
        await shot;
        await notReady;
        Assert.Equal(0, handler.PostCount);
    }

    private static WledShotEffectSink CreateSink(
        IWledOutput output,
        WledHttpStateAnimationManager animationManager,
        WledBallReadyDrgbController readyDrgb) =>
        new(
            () => new WledConfig
            {
                Brightness = 180,
                LedCount = 8,
                ControllerIp = "192.168.86.40"
            },
            output,
            animationManager,
            readyDrgb);

    private static WledHttpStateAnimationManager CreateAnimationManager(RecordingHttpHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new WledHttpStateAnimationManager(new WledDeviceClient(http));
    }

    private static ShotPayload SampleShot(double hla = 0) =>
        new()
        {
            BallData = new BallData
            {
                Speed = 140,
                SideSpin = 0,
                Hla = hla,
                CarryDistance = 200
            },
            MeasuredSmashFactor = 1.5,
            ShotDataOptions = new ShotDataOptions { ContainsBallData = true }
        };

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return;
            await Task.Delay(15);
        }

        Assert.Fail("Timed out waiting for condition.");
    }
}
