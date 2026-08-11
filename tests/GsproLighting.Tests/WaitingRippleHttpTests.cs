using GsproLighting.Core.Config;
using GsproLighting.Wled;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WaitingRippleHttpTests
{
    [Fact]
    public async Task ApplyWaitingRippleAsync_PostsLiveFalseRippleFromCatalog()
    {
        var handler = new RecordingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplyWaitingRippleAsync(
            "192.168.86.40",
            brightness: 180,
            color: WledShotEffectSink.WaitingColor);

        Assert.Equal(1, handler.GetCount);
        Assert.Equal(1, handler.PostCount);
        Assert.Contains("\"fx\":2", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"live\":false", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"on\":true", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains($"\"sx\":{EffectConfig.RippleTimingByte}", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains($"\"ix\":{EffectConfig.RippleTimingByte}", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyWaitingRippleAsync_HonorsStatusTuningSpeedIntensityLayers()
    {
        var handler = new RecordingHttpHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);
        var tuning = new StatusEffectStateTuning
        {
            Speed = 2.0,
            Intensity = 0.5,
            Layers = 0.5
        };

        await manager.ApplyWaitingRippleAsync(
            "192.168.86.40",
            brightness: 200,
            color: WledShotEffectSink.WaitingColor,
            tuning: tuning);

        Assert.Contains("\"sx\":76", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"ix\":19", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"bri\":100", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public void BandWidthsPerSecond_IsSlowedBreathingPace()
    {
        Assert.Equal(0.9, Wled.Animations.DrgbBandShimmerEffect.BandWidthsPerSecond);
        Assert.True(Wled.Animations.DrgbBandShimmerEffect.BandWidthsPerSecond < 2.0);
    }
}
