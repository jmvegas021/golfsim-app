using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using Xunit;

namespace GsproLighting.Tests;

public sealed class PreviewHoldKeepaliveTests
{
    [Fact]
    public async Task HoldAsync_FiniteDurationStopsWithoutCancel()
    {
        var output = new RecordingOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(30) };
        var plan = new PreviewHoldPlanFactory().Create(
            new LightingPreviewItem
            {
                Id = LightingPreviewIds.Water,
                Title = "Water",
                Description = "test",
                Slot = EffectSlot.Curated(RgbColor.FromRgb(0, 180, 180), EffectAnimations.Flash),
                HoldAsSolid = true
            },
            new WledConfig { Brightness = 160, LedCount = 12 });

        await keepalive.HoldAsync(
            output,
            plan,
            new WledConfig { Brightness = 160, LedCount = 12 },
            duration: TimeSpan.FromMilliseconds(70));

        Assert.True(output.SolidCount >= 2);
    }

    [Fact]
    public async Task HoldAsync_SupersedeCancelStopsLoop()
    {
        var output = new RecordingOutput();
        var keepalive = new PreviewHoldKeepalive { Interval = TimeSpan.FromMilliseconds(50) };
        var plan = new PreviewHoldPlanFactory().Create(
            new LightingPreviewItem
            {
                Id = LightingPreviewIds.Ready,
                Title = "Ready",
                Description = "test",
                Slot = EffectSlot.Curated(RgbColor.FromRgb(20, 80, 40), EffectAnimations.Solid)
            },
            new WledConfig());

        using var cts = new CancellationTokenSource(40);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await keepalive.HoldAsync(output, plan, new WledConfig { LedCount = 8 }, duration: null, cts.Token));
    }

    private sealed class RecordingOutput : IWledOutput
    {
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
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
