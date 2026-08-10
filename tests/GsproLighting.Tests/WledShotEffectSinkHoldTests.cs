using System.Net;
using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledShotEffectSinkHoldTests
{
    [Fact]
    public async Task OnBallReadyAsync_PostsEdgesInThenChaseToCenterHold()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.OnBallReadyAsync(new ShotPayload());

        // LedCount 8 → 8 edges-in + 8 chase frames.
        Assert.Equal(16, handler.PostCount);
        Assert.Contains("\"fx\":0", handler.LastBody);
        Assert.Contains("[0,220,0]", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        // First frame: green on both edges.
        Assert.Contains("\"start\":0", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":1", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"start\":7", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":8", handler.Bodies[0], StringComparison.Ordinal);
        // Hold is a center band smaller than the full strip.
        var holdLit = WledHttpReadyAnimationBuilder.ResolveHoldLitCount(8);
        Assert.True(holdLit < 8);
        Assert.Contains($"\"stop\":{(8 - holdLit) / 2 + holdLit}", handler.LastBody);
    }

    [Fact]
    public async Task OnBallNotReadyAsync_ExpandsThenBreathesUntilReadySupersedesIt()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(brightness: 180),
            animationManager);

        var breathing = sink.OnBallNotReadyAsync();
        await handler.WaitForPostsAsync(2);
        await sink.OnBallReadyAsync(new ShotPayload());
        await breathing;

        Assert.Contains(handler.Bodies, body => body.Contains("[180,30,30]", StringComparison.Ordinal));
        Assert.Contains("\"start\":3", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"bri\":180", handler.LastBody);
        Assert.Contains("[0,220,0]", handler.LastBody);
    }

    [Fact]
    public async Task OnShotAsync_PostsHitDirectionCenterOut_NoFollowUpIdle()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.OnShotAsync(SampleShot());
        await Task.Delay(100);

        // LedCount 8 → 8 expand steps + 1 hold for center green.
        Assert.Equal(9, handler.PostCount);
        Assert.Contains("[0,220,0]", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        Assert.Contains("\"start\":0", handler.LastBody);
        Assert.Contains("\"stop\":8", handler.LastBody);
    }

    [Fact]
    public async Task OnShotAsync_LeftHla_PostsLeftHalfYellow()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.OnShotAsync(SampleShot(hla: -6));

        Assert.True(handler.PostCount > 1);
        Assert.Contains("[220,180,0]", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"start\":3", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":4", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnShotAsync_RespectsInvertLeftRight()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(invertLeftRight: true),
            animationManager);

        // Negative HLA is left; invert plays right animation (grows from center to right).
        await sink.OnShotAsync(SampleShot(hla: -6));

        Assert.Contains("\"start\":4", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"stop\":5", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnPlayerInfoAsync_PostsBlueOnce()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.OnPlayerInfoAsync(new GsproResponse { Code = 201 });

        Assert.Equal(1, handler.PostCount);
        Assert.Contains("[40,120,255]", handler.LastBody);
    }

    [Fact]
    public async Task HoldWaitingAsync_DoesNotPost()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.HoldWaitingAsync();

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task HoldIdleForConnectionChangeAsync_DoesNotPost()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.HoldIdleForConnectionChangeAsync();

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnBallReadyAsync_SkipsWhenControllerNotConfigured()
    {
        var handler = new RecordingHttpHandler();
        using var animationManager = CreateAnimationManager(handler);
        var sink = new WledShotEffectSink(
            () => new WledConfig { ControllerIp = WledConfig.DefaultControllerIp, Brightness = 180 },
            animationManager);

        await sink.OnBallReadyAsync(new ShotPayload());

        Assert.Equal(0, handler.PostCount);
    }

    [Fact]
    public async Task OnBallReadyAsync_HttpFailure_DoesNotThrow_AndLogs()
    {
        var handler = new FailingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var animationManager = new WledHttpStateAnimationManager(client);
        var logs = new List<string>();
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager,
            logFailure: logs.Add);

        await sink.OnBallReadyAsync(new ShotPayload());

        Assert.NotEmpty(logs);
        Assert.Contains(logs, line => line.Contains("WLED effect failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnShotAsync_HttpFailure_DoesNotThrow()
    {
        var handler = new FailingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var animationManager = new WledHttpStateAnimationManager(client);
        var sink = new WledShotEffectSink(
            () => ConfiguredWled(),
            animationManager);

        await sink.OnShotAsync(SampleShot());
    }

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
