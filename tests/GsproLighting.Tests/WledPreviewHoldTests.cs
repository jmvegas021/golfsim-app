using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledPreviewHoldTests
{
    [Fact]
    public async Task PreviewAndHoldAsync_HoldsSolidAfterAnimationWithoutClear()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var plan = CreateReadyPlan(180);

        await player.PreviewAndHoldAsync(plan, new WledConfig { Brightness = 180 }, holdDuration: null);

        Assert.NotEmpty(output.SolidHolds);
        Assert.Equal(20, output.SolidHolds.Last().Color.R);
        Assert.Empty(output.Clears);
    }

    [Fact]
    public async Task PreviewAndHoldAsync_SecondPreviewSupersedesFirst()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var factory = new PreviewHoldPlanFactory();
        var waiting = factory.Create(new LightingPreviewItem
        {
            Id = LightingPreviewIds.Waiting,
            Title = "Waiting",
            Description = "test",
            Slot = EffectSlot.Curated(RgbColor.FromRgb(180, 120, 20), EffectAnimations.Solid),
            HoldBrightnessFactor = 0.33
        }, new WledConfig());
        var ready = factory.Create(new LightingPreviewItem
        {
            Id = LightingPreviewIds.Ready,
            Title = "Ready",
            Description = "test",
            Slot = EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.Solid)
        }, new WledConfig());

        await player.PreviewAndHoldAsync(waiting, new WledConfig());
        await player.PreviewAndHoldAsync(ready, new WledConfig());

        Assert.Contains(output.SolidHolds, hold => hold.Color.R == 20 && hold.Color.G == 80);
    }

    [Fact]
    public async Task PreviewAndHoldAsync_KeepaliveResendsWhileHolding()
    {
        var output = new RecordingWledOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(40) };
        var plan = CreateReadyPlan(200);
        using var cts = new CancellationTokenSource();

        var holdTask = keepalive.HoldAsync(
            output,
            plan,
            new WledConfig { Brightness = 200, LedCount = 8 },
            duration: null,
            cts.Token);

        await Task.Delay(130);
        Assert.True(output.SolidHolds.Count >= 3, $"Expected keepalive resends, got {output.SolidHolds.Count}");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await holdTask);
    }

    [Fact]
    public async Task PreviewAndHoldAsync_CancelTokenStopsFiniteHold()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var plan = CreateReadyPlan(180);
        using var cts = new CancellationTokenSource(60);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await player.PreviewAndHoldAsync(
                plan,
                new WledConfig { Brightness = 180 },
                holdDuration: TimeSpan.FromSeconds(10),
                cts.Token));
    }

    [Fact]
    public async Task PreviewAndHoldAsync_CancelActivePreviewStopsKeepalive()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var plan = CreateReadyPlan(180);

        await player.PreviewAndHoldAsync(plan, new WledConfig { Brightness = 180 }, holdDuration: null);
        var countAfterHold = output.SolidHolds.Count;
        player.CancelActivePreview();
        await Task.Delay(80);
        Assert.Equal(countAfterHold, output.SolidHolds.Count);
    }

    [Fact]
    public async Task PreviewAndHoldAsync_OnHoldStartedFiresBeforeReturn()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var plan = CreateReadyPlan(180);
        var holdStarted = false;

        await player.PreviewAndHoldAsync(
            plan,
            new WledConfig { Brightness = 180 },
            holdDuration: TimeSpan.FromMilliseconds(30),
            onHoldStarted: () => holdStarted = true);

        Assert.True(holdStarted);
        Assert.Empty(output.Clears);
    }

    [Fact]
    public async Task StopAndHoldIdleAsync_SendsIdleSolid()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var idle = EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.CenterToOutside);

        await player.StopAndHoldIdleAsync(idle, new WledConfig { Brightness = 200 });

        Assert.NotEmpty(output.SolidHolds);
        Assert.Equal(20, output.SolidHolds[0].Color.R);
        Assert.Equal((byte)200, output.SolidHolds[0].Brightness);
    }

    [Fact]
    public async Task StopAndHoldIdleAsync_SupersedesActiveHold()
    {
        var output = new RecordingWledOutput();
        using var player = new WledPreviewPlayer(output);
        var plan = CreateReadyPlan(180);

        await player.PreviewAndHoldAsync(plan, new WledConfig { Brightness = 180 });
        await player.StopAndHoldIdleAsync(
            EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.Solid),
            new WledConfig { Brightness = 200 });

        Assert.Contains(output.SolidHolds, hold => hold.Brightness == 200);
        Assert.Empty(output.Clears);
    }

    private static PreviewHoldPlan CreateReadyPlan(byte brightness) =>
        new PreviewHoldPlanFactory().Create(
            new LightingPreviewItem
            {
                Id = LightingPreviewIds.Ready,
                Title = "Ready",
                Description = "test",
                Slot = EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.Solid),
                HoldAsSolid = true,
                HoldBrightnessFactor = 1
            },
            new WledConfig { Brightness = brightness });

    private sealed class RecordingWledOutput : IWledOutput
    {
        public List<(RgbColor Color, byte? Brightness)> SolidHolds { get; } = [];
        public List<int> Clears { get; } = [];

        public void Configure(WledConfig config)
        {
        }

        public Task SendSolidAsync(
            RgbColor color,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            SolidHolds.Add((color, brightness));
            return Task.CompletedTask;
        }

        public Task SendPixelsAsync(
            IReadOnlyList<RgbColor> pixels,
            byte? brightness = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Clears.Add(1);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
