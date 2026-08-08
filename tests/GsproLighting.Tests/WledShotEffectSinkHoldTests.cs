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

        await Task.Delay(200);
        Assert.True(
            output.SolidCountFor(61, 220, 132) >= 3,
            $"Expected idle keepalive resends, got {output.SolidCountFor(61, 220, 132)}");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await readyTask);
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
        return effects;
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
