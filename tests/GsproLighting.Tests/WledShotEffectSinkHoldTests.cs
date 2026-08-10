using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using GsproLighting.Wled.Device;
using Xunit;

// Recording helpers live in WledSinkTestFixtures.cs

namespace GsproLighting.Tests;

public sealed class WledShotEffectSinkHoldTests
{
    [Fact]
    public async Task OnBallReadyAsync_StreamsDrgbEdgesInThenShimmerCenterBand()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);
        var band = DrgbConcentrateBandGeometry.ResolveCenter(8);

        var ready = sink.OnBallReadyAsync(new ShotPayload());
        await WaitUntilAsync(
            () =>
            {
                var frames = output.SnapshotFrames();
                return frames.Length >= 2 &&
                       frames.Any(frame => HasBandHue(frame, band, DrgbReadyFrameFactory.ReadyGreen)) &&
                       HasDistinctHoldFrames(frames, band, DrgbReadyFrameFactory.ReadyGreen);
            },
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await ready;

        var frames = output.SnapshotFrames();
        Assert.Equal(0, handler.PostCount);
        Assert.True(IsBlack(frames[0]));
        Assert.Equal(DrgbReadyFrameFactory.ReadyGreen, frames[1][0]);
        Assert.Equal(DrgbReadyFrameFactory.ReadyGreen, frames[1][^1]);
        Assert.Contains(
            frames,
            frame => HasBandHue(frame, band, DrgbReadyFrameFactory.ReadyGreen) &&
                     IsOutsideBandBlack(frame, band));
    }

    [Fact]
    public async Task OnBallNotReadyAsync_ExpandsOnDrgbUntilReadySupersedesIt()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager, brightness: 180);

        var notReady = sink.OnBallNotReadyAsync();
        await WaitUntilAsync(() => output.FrameCount >= 2, TimeSpan.FromSeconds(3));
        var ready = sink.OnBallReadyAsync(new ShotPayload());
        await notReady;
        var band = DrgbConcentrateBandGeometry.ResolveCenter(8);
        await WaitUntilAsync(
            () => output.SnapshotFrames().Any(frame =>
                HasBandHue(frame, band, DrgbReadyFrameFactory.ReadyGreen)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await ready;

        Assert.Equal(0, handler.PostCount);
        Assert.Contains(
            output.SnapshotFrames(),
            frame => frame.Any(pixel =>
                pixel.R == DrgbNotReadyFrameFactory.NotReadyRed.R &&
                pixel.G == 0 &&
                pixel.B == 0));
    }

    [Fact]
    public async Task OnShotAsync_StreamsDirectionCenterBandShimmer_NoHttp()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);
        var band = DrgbConcentrateBandGeometry.ResolveCenter(8);

        var shot = sink.OnShotAsync(SampleShot());
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionCenter &&
                  output.SnapshotFrames().Any(frame =>
                      HasBandHue(frame, band, DrgbDirectionFrameFactory.DirectionCenterGreen) &&
                      IsOutsideBandBlack(frame, band)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnShotAsync_SupersedesReadyDrgbWithDirectionHold()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);

        var ready = sink.OnBallReadyAsync(new ShotPayload());
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.ReadyCenterBand,
            TimeSpan.FromSeconds(3));

        var shot = sink.OnShotAsync(SampleShot());
        await ready;
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionCenter,
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;

        Assert.Equal(0, handler.PostCount);
        Assert.Equal(WledBallReadyDrgbController.HeldPose.None, readyDrgb.CurrentPose);
    }

    [Fact]
    public async Task OnShotAsync_LeftHla_StreamsLeftYellowBandShimmer()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);
        var band = DrgbConcentrateBandGeometry.ResolveLeft(8);

        var shot = sink.OnShotAsync(SampleShot(hla: -6));
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionLeft &&
                  output.SnapshotFrames().Any(frame =>
                      HasBandHue(frame, band, DrgbDirectionFrameFactory.DirectionSideYellow) &&
                      IsOutsideBandBlack(frame, band)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnShotAsync_RespectsInvertLeftRight()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, invertLeftRight: true, readyDrgb: readyDrgb);

        var shot = sink.OnShotAsync(SampleShot(hla: -6));
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionRight,
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnBallReadyAsync_SupersedesDirectionHold_AfterMinHold()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(60));
        using var readyDrgb = new WledBallReadyDrgbController(output, gate);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);
        var band = DrgbConcentrateBandGeometry.ResolveCenter(8);

        var shot = sink.OnShotAsync(SampleShot(hla: -6));
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.DirectionLeft,
            TimeSpan.FromSeconds(3));

        var ready = sink.OnBallReadyAsync(new ShotPayload());
        Assert.Equal(WledBallReadyDrgbController.HeldPose.DirectionLeft, readyDrgb.CurrentPose);

        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.ReadyCenterBand &&
                  output.SnapshotFrames().Any(frame =>
                      HasBandHue(frame, band, DrgbReadyFrameFactory.ReadyGreen)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await shot;
        await ready;

        Assert.Equal(0, handler.PostCount);
        Assert.Equal(TimeSpan.FromSeconds(4), DrgbDirectionMinHoldGate.DefaultMinHold);
    }

    [Fact]
    public async Task OnPlayerInfoAsync_StreamsWaitingAquaDrgb_NoHttp()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);

        var waiting = sink.OnPlayerInfoAsync(new GsproResponse { Code = 201 });
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.WaitingAqua &&
                  output.SnapshotFrames().Any(frame =>
                      frame.Any(pixel => pixel.R == 0 && pixel.G > 0 && pixel.B > 0)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await waiting;

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task HoldWaitingAsync_StreamsAquaDrgb_NoHttp()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);

        var waiting = sink.HoldWaitingAsync();
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.WaitingAqua,
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await waiting;

        Assert.Equal(0, handler.PostCount);
        Assert.True(output.FrameCount > 0);
    }

    [Fact]
    public async Task HoldIdleForConnectionChangeAsync_DoesNotPost()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        await sink.HoldIdleForConnectionChangeAsync();

        Assert.Equal(0, handler.PostCount);
        Assert.Equal(0, output.FrameCount);
    }

    [Fact]
    public async Task OnBallReadyAsync_SkipsWhenControllerNotConfigured()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = new WledShotEffectSink(
            () => new WledConfig { ControllerIp = WledConfig.DefaultControllerIp, Brightness = 180 },
            output,
            animationManager);

        await sink.OnBallReadyAsync(new ShotPayload());

        Assert.Equal(0, handler.PostCount);
        Assert.Equal(0, output.FrameCount);
    }

    [Fact]
    public async Task OnPlayerInfoAsync_DoesNotThrowWhenHttpClientBroken()
    {
        var output = new RecordingWledOutput();
        var handler = new FailingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var animationManager = new WledHttpStateAnimationManager(client);
        using var readyDrgb = new WledBallReadyDrgbController(output);
        using var sink = CreateSink(output, animationManager, readyDrgb: readyDrgb);

        var waiting = sink.OnPlayerInfoAsync(new GsproResponse { Code = 201 });
        await WaitUntilAsync(
            () => readyDrgb.CurrentPose == WledBallReadyDrgbController.HeldPose.WaitingAqua,
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await waiting;
    }

    private static WledShotEffectSink CreateSink(
        IWledOutput output,
        WledHttpStateAnimationManager animationManager,
        byte brightness = 180,
        bool invertLeftRight = false,
        WledBallReadyDrgbController? readyDrgb = null) =>
        new(
            () => ConfiguredWled(brightness, invertLeftRight),
            output,
            animationManager,
            readyDrgb);

    private static WledHttpStateAnimationManager CreateAnimationManager(RecordingHttpHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new WledHttpStateAnimationManager(new WledDeviceClient(http));
    }

    private static WledConfig ConfiguredWled(
        byte brightness = 180,
        bool invertLeftRight = false) =>
        new()
        {
            Brightness = brightness,
            LedCount = 8,
            ControllerIp = "192.168.86.40",
            InvertLeftRight = invertLeftRight
        };

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

    private static bool HasDistinctHoldFrames(
        IReadOnlyList<RgbColor>[] frames,
        LedBandRange band,
        RgbColor color)
    {
        var hold = frames
            .Where(frame => HasBandHue(frame, band, color) && IsOutsideBandBlack(frame, band))
            .TakeLast(2)
            .ToArray();
        return hold.Length == 2 && !hold[0].SequenceEqual(hold[1]);
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
            if (IsBlack(pixel))
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

    private static bool IsOutsideBandBlack(IReadOnlyList<RgbColor> frame, LedBandRange band)
    {
        for (var i = 0; i < frame.Count; i++)
        {
            if (i >= band.Start && i < band.EndExclusive)
                continue;
            if (!IsBlack(frame[i]))
                return false;
        }

        return true;
    }

    private static bool IsBlack(IReadOnlyList<RgbColor> pixels) =>
        pixels.All(IsBlack);

    private static bool IsBlack(RgbColor pixel) =>
        pixel.R == 0 && pixel.G == 0 && pixel.B == 0;

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
