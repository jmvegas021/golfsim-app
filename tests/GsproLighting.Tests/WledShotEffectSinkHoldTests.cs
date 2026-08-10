using System.Net;
using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledShotEffectSinkHoldTests
{
    [Fact]
    public async Task OnBallReadyAsync_StreamsDrgbEdgesInThenHoldsCenterBand()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        var ready = sink.OnBallReadyAsync(new ShotPayload());
        var hold = DrgbReadyFrameFactory.CreateHoldPixels(8);
        await WaitUntilAsync(
            () => output.PixelFrames.Any(frame => frame.SequenceEqual(hold)),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await ready;

        Assert.Equal(0, handler.PostCount);
        Assert.True(IsBlack(output.PixelFrames[0]));
        Assert.Equal(DrgbReadyFrameFactory.ReadyGreen, output.PixelFrames[1][0]);
        Assert.Equal(DrgbReadyFrameFactory.ReadyGreen, output.PixelFrames[1][^1]);
        Assert.Contains(output.PixelFrames, frame => frame.SequenceEqual(hold));
    }

    [Fact]
    public async Task OnBallNotReadyAsync_ExpandsOnDrgbUntilReadySupersedesIt()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager, brightness: 180);

        var notReady = sink.OnBallNotReadyAsync();
        await WaitUntilAsync(() => output.PixelFrames.Count >= 2, TimeSpan.FromSeconds(3));
        var ready = sink.OnBallReadyAsync(new ShotPayload());
        await notReady;
        await WaitUntilAsync(
            () => output.PixelFrames.Any(frame =>
                frame.SequenceEqual(DrgbReadyFrameFactory.CreateHoldPixels(8))),
            TimeSpan.FromSeconds(3));
        sink.CancelActiveEffects();
        await ready;

        Assert.Equal(0, handler.PostCount);
        Assert.Contains(
            output.PixelFrames,
            frame => frame.Any(pixel =>
                pixel.R == DrgbNotReadyFrameFactory.NotReadyRed.R &&
                pixel.G == DrgbNotReadyFrameFactory.NotReadyRed.G &&
                pixel.B == DrgbNotReadyFrameFactory.NotReadyRed.B));
    }

    [Fact]
    public async Task OnShotAsync_PostsHitDirectionCenterOut_NoFollowUpIdle()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        await sink.OnShotAsync(SampleShot());
        await Task.Delay(100);

        var expected = WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(8) + 1;
        Assert.Equal(expected, handler.PostCount);
        Assert.Contains("[0,220,0]", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        Assert.Contains("\"start\":0", handler.LastBody);
        Assert.Contains("\"stop\":8", handler.LastBody);
    }

    [Fact]
    public async Task OnShotAsync_CancelsReadyDrgbBeforeHttpHitDirection()
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

        await sink.OnShotAsync(SampleShot());
        await ready;

        Assert.Equal(WledBallReadyDrgbController.HeldPose.None, readyDrgb.CurrentPose);
        Assert.True(handler.PostCount > 0);
        Assert.Contains("\"live\":false", handler.LastBody);
    }

    [Fact]
    public async Task OnShotAsync_LeftHla_PostsLeftHalfYellow()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        await sink.OnShotAsync(SampleShot(hla: -6));

        Assert.True(handler.PostCount > 1);
        Assert.Contains("[220,180,0]", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"start\":3", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":4", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnShotAsync_RespectsInvertLeftRight()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager, invertLeftRight: true);

        await sink.OnShotAsync(SampleShot(hla: -6));

        Assert.Contains("\"start\":4", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":5", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnPlayerInfoAsync_PostsBlueOnce()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        await sink.OnPlayerInfoAsync(new GsproResponse { Code = 201 });

        Assert.Equal(1, handler.PostCount);
        Assert.Contains("[40,120,255]", handler.LastBody);
    }

    [Fact]
    public async Task HoldWaitingAsync_DoesNotPost()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        using var sink = CreateSink(output, animationManager);

        await sink.HoldWaitingAsync();

        Assert.Equal(0, handler.PostCount);
        Assert.Empty(output.PixelFrames);
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
        Assert.Empty(output.PixelFrames);
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
        Assert.Empty(output.PixelFrames);
    }

    [Fact]
    public async Task OnShotAsync_HttpFailure_DoesNotThrow()
    {
        var output = new RecordingWledOutput();
        var handler = new FailingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var animationManager = new WledHttpStateAnimationManager(client);
        using var sink = CreateSink(output, animationManager);

        await sink.OnShotAsync(SampleShot());
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
            MeasuredSmashFactor = 1.5
        };

    private static bool IsBlack(IReadOnlyList<RgbColor> pixels) =>
        pixels.All(pixel => pixel.R == 0 && pixel.G == 0 && pixel.B == 0);

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

    private sealed class RecordingWledOutput : IWledOutput
    {
        public List<IReadOnlyList<RgbColor>> PixelFrames { get; } = [];

        public void Configure(WledConfig config)
        {
        }

        public Task SendSolidAsync(
            RgbColor color,
            byte? brightness = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendPixelsAsync(
            IReadOnlyList<RgbColor> pixels,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            PixelFrames.Add(pixels.ToArray());
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _twoPosts =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PostCount { get; private set; }
        public string LastBody { get; private set; } = "";
        public List<string> Bodies { get; } = [];

        public Task WaitForPostsAsync(int count)
        {
            if (PostCount >= count)
                return Task.CompletedTask;
            return _twoPosts.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            PostCount++;
            LastBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(LastBody);
            if (PostCount >= 2)
                _twoPosts.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }

    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated WLED controller unreachable");
    }
}
