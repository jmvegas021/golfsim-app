using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledChaseAuroraStateFactoryTests
{
    [Fact]
    public void CreateReadyBody_IsChaseAuroraAtMax_OnCenterBandWithBlackFlanks()
    {
        const int ledCount = 120;
        var body = WledChaseAuroraStateFactory.CreateReadyBody(ledCount, brightness: 200);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        var start = (ledCount - concentrate) / 2;
        var stop = start + concentrate;

        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(200, root.GetProperty("bri").GetInt32());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.Equal(1, root.GetProperty("mainseg").GetInt32());

        var segs = root.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(3 + WledAuthoritativeStateFactory.ExtraSegmentsToClear - 2, segs.Length);
        Assert.True(segs.Length >= 3);
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(start, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal([0, 0, 0], ReadPrimary(segs[0]));

        Assert.Equal(start, segs[1].GetProperty("start").GetInt32());
        Assert.Equal(stop, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(EffectConfig.ChaseFxId, segs[1].GetProperty("fx").GetInt32());
        Assert.Equal(EffectConfig.MaxTimingByte, segs[1].GetProperty("sx").GetInt32());
        Assert.Equal(EffectConfig.MaxTimingByte, segs[1].GetProperty("ix").GetInt32());
        Assert.Equal(EffectConfig.AuroraPaletteId, segs[1].GetProperty("pal").GetInt32());
        Assert.Equal(50, segs[1].GetProperty("pal").GetInt32());
        Assert.Equal([0, 220, 0], ReadPrimary(segs[1]));

        Assert.Equal(stop, segs[2].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, segs[2].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[2].GetProperty("fx").GetInt32());
        Assert.Equal(0, segs[3].GetProperty("stop").GetInt32());
    }

    [Fact]
    public void CreateNotReadyBody_IsRedChaseAtMax_WithRedReef_WithoutAurora()
    {
        var body = WledChaseAuroraStateFactory.CreateNotReadyBody(ledCount: 80, brightness: 180);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;
        var segs = root.GetProperty("seg").EnumerateArray().ToArray();

        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(0, root.GetProperty("mainseg").GetInt32());
        Assert.Equal(EffectConfig.ChaseFxId, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(28, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("sx").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("ix").GetInt32());
        Assert.Equal(EffectConfig.RedReefPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal(62, segs[0].GetProperty("pal").GetInt32());
        Assert.NotEqual(EffectConfig.AuroraPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimary(segs[0]));
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(80, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[2].GetProperty("stop").GetInt32());
    }

    private static int[] ReadPrimary(JsonElement segment) =>
        segment.GetProperty("col")[0].EnumerateArray().Select(v => v.GetInt32()).ToArray();
}
