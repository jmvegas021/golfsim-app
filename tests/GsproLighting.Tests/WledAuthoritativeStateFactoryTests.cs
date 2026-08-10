using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledAuthoritativeStateFactoryTests
{
    [Fact]
    public void CreateFullStripBody_ClearsPresetsPlaylistsLiveAndExtraSegments()
    {
        var body = WledAuthoritativeStateFactory.CreateFullStripBody(
            ledCount: 100,
            brightness: 200,
            fxId: EffectConfig.ChaseFxId,
            speed: EffectConfig.MaxTimingByte,
            intensity: EffectConfig.MaxTimingByte,
            paletteId: EffectConfig.AuroraPaletteId,
            primary: RgbColor.FromRgb(0, 220, 0));
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;

        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.Equal(200, root.GetProperty("bri").GetInt32());
        Assert.Equal(0, root.GetProperty("tt").GetInt32());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.Equal(0, root.GetProperty("mainseg").GetInt32());

        var segs = root.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(1 + WledAuthoritativeStateFactory.ExtraSegmentsToClear, segs.Length);
        Assert.Equal(0, segs[0].GetProperty("id").GetInt32());
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(100, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(EffectConfig.ChaseFxId, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(EffectConfig.MaxTimingByte, segs[0].GetProperty("sx").GetInt32());
        Assert.Equal(EffectConfig.MaxTimingByte, segs[0].GetProperty("ix").GetInt32());
        Assert.Equal(EffectConfig.AuroraPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.False(segs[0].GetProperty("o1").GetBoolean());
        Assert.False(segs[0].GetProperty("o2").GetBoolean());
        Assert.False(segs[0].GetProperty("o3").GetBoolean());
        Assert.Equal(
            [0, 220, 0],
            segs[0].GetProperty("col")[0].EnumerateArray().Select(v => v.GetInt32()).ToArray());
        Assert.Equal(
            [0, 0, 0],
            segs[0].GetProperty("col")[1].EnumerateArray().Select(v => v.GetInt32()).ToArray());
        Assert.Equal(
            [0, 0, 0],
            segs[0].GetProperty("col")[2].EnumerateArray().Select(v => v.GetInt32()).ToArray());

        for (var i = 1; i < segs.Length; i++)
        {
            Assert.Equal(i, segs[i].GetProperty("id").GetInt32());
            Assert.Equal(0, segs[i].GetProperty("stop").GetInt32());
        }
    }

    [Fact]
    public void CreateSolidBody_IsFx0FullStripAuthoritative()
    {
        var body = WledAuthoritativeStateFactory.CreateSolidBody(
            ledCount: 60,
            RgbColor.FromRgb(255, 255, 255),
            brightness: 180);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;
        var seg0 = root.GetProperty("seg")[0];

        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(0, root.GetProperty("mainseg").GetInt32());
        Assert.Equal(0, seg0.GetProperty("fx").GetInt32());
        Assert.Equal(0, seg0.GetProperty("pal").GetInt32());
        Assert.Equal(60, seg0.GetProperty("stop").GetInt32());
        Assert.Equal(0, root.GetProperty("seg")[1].GetProperty("stop").GetInt32());
    }

    [Fact]
    public void CreateOffBody_StopsLivePresetsAndPlaylists()
    {
        var body = WledAuthoritativeStateFactory.CreateOffBody();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;

        Assert.False(root.GetProperty("on").GetBoolean());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.False(root.TryGetProperty("seg", out _));
    }

    [Fact]
    public void CreateBody_ClearsUnusedExtraSegmentsOnly()
    {
        var body = WledAuthoritativeStateFactory.CreateBody(
            brightness: 100,
            [
                WledAuthoritativeStateFactory.CreateSegment(
                    0, 0, 4, 0, 128, 128, 0, RgbColor.FromRgb(0, 0, 0)),
                WledAuthoritativeStateFactory.CreateSegment(
                    1, 4, 8, 0, 128, 128, 0, RgbColor.FromRgb(255, 0, 0)),
                WledAuthoritativeStateFactory.CreateSegment(
                    2, 8, 12, 0, 128, 128, 0, RgbColor.FromRgb(0, 0, 0))
            ]);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var segs = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();

        Assert.Equal(3 + (WledAuthoritativeStateFactory.ExtraSegmentsToClear - 2), segs.Length);
        Assert.Equal(4, segs[1].GetProperty("start").GetInt32());
        Assert.Equal(8, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(3, segs[3].GetProperty("id").GetInt32());
        Assert.Equal(0, segs[3].GetProperty("stop").GetInt32());
        Assert.Equal(9, segs[^1].GetProperty("id").GetInt32());
    }
}
