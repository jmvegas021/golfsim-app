using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledShotEffectSinkHoldTests
{
    [Fact]
    public async Task OnBallReadyAsync_KeepaliveResendsIdleSolid()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(40) };
        var effects = FastHoldEffects();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        using var cts = new CancellationTokenSource();
        var readyTask = sink.OnBallReadyAsync(new ShotPayload(), cts.Token);

        // Ready intro (CenterToOutside) runs before idle solid keepalive.
        await Task.Delay(450);
        Assert.True(
            output.SolidCountFor(61, 220, 132) >= 3,
            $"Expected idle keepalive resends, got {output.SolidCountFor(61, 220, 132)}");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readyTask);
    }

    [Fact]
    public async Task OnBallReadyAsync_RippleIdle_ReappliesHttpPreset()
    {
        var output = new RecordingWledOutput();
        var handler = new RecordingHttpHandler();
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(40) };
        var effects = new EffectConfig();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 200, LedCount = 8, ControllerIp = "192.168.1.50" },
            keepalive,
            http);

        using var cts = new CancellationTokenSource();
        var readyTask = sink.OnBallReadyAsync(new ShotPayload(), cts.Token);

        // Intro animation then Ripple HTTP hold with keepalive re-apply.
        await Task.Delay(450);
        Assert.True(handler.PostCount >= 2, $"Expected Ripple HTTP re-apply, got {handler.PostCount}");
        Assert.Contains(handler.Bodies, body => body.Contains("\"fx\":79") && body.Contains("\"pal\":62"));

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readyTask);
    }

    [Fact]
    public async Task OnBallReadyAsync_HttpIdleFailure_DoesNotThrow_AndLogs()
    {
        var output = new RecordingWledOutput();
        var handler = new FailingHttpHandler();
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var logs = new List<string>();
        var effects = new EffectConfig();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 200, LedCount = 8, ControllerIp = "192.168.1.50" },
            httpClient: http,
            logFailure: logs.Add);

        // Must complete without throwing — proxy pipes depend on this.
        await sink.OnBallReadyAsync(new ShotPayload());

        Assert.NotEmpty(logs);
        Assert.Contains(logs, line => line.Contains("WLED effect failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnShotAsync_HttpIdleFailure_DoesNotThrow()
    {
        var output = new RecordingWledOutput();
        var handler = new FailingHttpHandler();
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var effects = FastHoldEffects();
        effects.Idle = EffectConfig.CreateRippleAmbient(RgbColor.FromRgb(61, 220, 132));
        effects.PureStrike = EffectSlot.Curated(
            RgbColor.FromRgb(0, 224, 90),
            EffectAnimations.Solid);
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8, ControllerIp = "192.168.1.50" },
            httpClient: http);

        await sink.OnShotAsync(
            new ShotPayload
            {
                BallData = new BallData
                {
                    Speed = 140,
                    SideSpin = 0,
                    Hla = 0,
                    CarryDistance = 200
                },
                MeasuredSmashFactor = 1.5
            });
    }

    [Fact]
    public async Task OnBallNotReadyAsync_KeepaliveResendsDimSolid()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(40) };
        var effects = FastHoldEffects();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        using var cts = new CancellationTokenSource();
        var notReadyTask = sink.OnBallNotReadyAsync(cts.Token);

        await Task.Delay(200);
        Assert.True(
            output.SolidCountFor(229, 83, 61) >= 3,
            $"Expected not-ready keepalive resends, got {output.SolidCountFor(229, 83, 61)}");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await notReadyTask);
    }

    [Fact]
    public async Task OnBallNotReadyAsync_SupersedesReadyKeepalive()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(30) };
        var effects = FastHoldEffects();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        var readyTask = sink.OnBallReadyAsync(new ShotPayload());
        await Task.Delay(90);
        var idleCountBefore = output.SolidCountFor(61, 220, 132);

        using var cts = new CancellationTokenSource();
        var notReadyTask = sink.OnBallNotReadyAsync(cts.Token);
        await Task.Delay(90);

        Assert.True(output.SolidCountFor(229, 83, 61) >= 1);
        await Task.Delay(90);
        Assert.Equal(idleCountBefore, output.SolidCountFor(61, 220, 132));

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await notReadyTask);
        await readyTask; // superseded by not-ready — completes without throw
    }

    [Fact]
    public async Task HoldWaitingAsync_KeepaliveResendsWaitingSolid_NotIdle()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(40) };
        var effects = FastHoldEffects();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        using var cts = new CancellationTokenSource();
        var waitingTask = sink.HoldWaitingAsync(cts.Token);

        await Task.Delay(200);
        Assert.True(
            output.SolidCountFor(212, 160, 23) >= 3,
            $"Expected waiting keepalive resends, got {output.SolidCountFor(212, 160, 23)}");
        Assert.Equal(0, output.SolidCountFor(61, 220, 132));

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waitingTask);
    }

    [Fact]
    public async Task OnBallReadyAsync_SupersedesWaitingHold()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(30) };
        var effects = FastHoldEffects();
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        var waitingTask = sink.HoldWaitingAsync();
        await Task.Delay(90);
        var waitingCountBefore = output.SolidCountFor(212, 160, 23);

        using var cts = new CancellationTokenSource();
        var readyTask = sink.OnBallReadyAsync(new ShotPayload(), cts.Token);
        await Task.Delay(450);

        Assert.True(output.SolidCountFor(61, 220, 132) >= 1);
        await Task.Delay(90);
        Assert.Equal(waitingCountBefore, output.SolidCountFor(212, 160, 23));

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readyTask);
        await waitingTask; // superseded by ready — completes without throw
    }

    [Fact]
    public async Task OnShotAsync_HoldsShotColorThenIdleKeepalive()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(35) };
        var effects = FastHoldEffects();
        effects.PureStrike = EffectSlot.Curated(
            RgbColor.FromRgb(0, 224, 90),
            EffectAnimations.Solid);
        var sink = new WledShotEffectSink(
            output,
            () => effects,
            () => new WledConfig { Brightness = 180, LedCount = 8 },
            keepalive);

        using var cts = new CancellationTokenSource();
        var shotTask = sink.OnShotAsync(
            new ShotPayload
            {
                BallData = new BallData
                {
                    Speed = 140,
                    SideSpin = 0,
                    Hla = 0,
                    CarryDistance = 200
                },
                MeasuredSmashFactor = 1.5
            },
            cts.Token);

        await Task.Delay(250);
        Assert.True(
            output.SolidCountFor(0, 224, 90) >= 2,
            "Expected pure-strike solid keepalive during post-shot hold");

        // Post-shot hold is 1600ms; wait for idle keepalive after that.
        await Task.Delay(2000);
        Assert.True(
            output.SolidCountFor(61, 220, 132) >= 2,
            $"Expected idle keepalive after post-shot hold, got {output.SolidCountFor(61, 220, 132)}");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shotTask);
    }

    private static EffectConfig FastHoldEffects()
    {
        var effects = new EffectConfig();
        effects.Idle = EffectSlot.Curated(
            RgbColor.FromRgb(61, 220, 132),
            EffectAnimations.Solid);
        effects.NotReady = EffectSlot.Curated(
            RgbColor.FromRgb(229, 83, 61),
            EffectAnimations.Solid);
        effects.Waiting = EffectSlot.Curated(
            RgbColor.FromRgb(212, 160, 23),
            EffectAnimations.Solid);
        return effects;
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public int PostCount { get; private set; }
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            PostCount++;
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
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

    private sealed class RecordingWledOutput : IWledOutput
    {
        private readonly object _gate = new();
        private readonly List<(byte R, byte G, byte B)> _solids = [];

        public int SolidCountFor(byte r, byte g, byte b)
        {
            lock (_gate)
                return _solids.Count(c => c.R == r && c.G == g && c.B == b);
        }

        public void Configure(WledConfig config)
        {
        }

        public Task SendSolidAsync(
            RgbColor color,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
                _solids.Add((color.R, color.G, color.B));
            return Task.CompletedTask;
        }

        public Task SendPixelsAsync(
            IReadOnlyList<RgbColor> pixels,
            byte? brightness = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
