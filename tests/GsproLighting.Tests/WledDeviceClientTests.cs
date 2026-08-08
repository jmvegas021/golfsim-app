using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledDeviceClientTests
{
    [Fact]
    public async Task ApplyStateAsync_RetriesOnceOn413_ThenSucceeds()
    {
        var handler = new SequencedStatusHandler([HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.OK]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);

        await client.ApplyStateAsync("192.168.1.50", new WledStatePatch { On = true });

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ApplyStateAsync_413Twice_ThrowsFriendlyMessage()
    {
        var handler = new SequencedStatusHandler(
            [HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestEntityTooLarge]);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ApplyStateAsync("192.168.1.50", new WledStatePatch { On = true }));

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("low on memory", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, ex.StatusCode);
    }

    private sealed class SequencedStatusHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;

        public SequencedStatusHandler(IEnumerable<HttpStatusCode> statuses) =>
            _statuses = new Queue<HttpStatusCode>(statuses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }

    [Fact]
    public void ParseNameArray_SkipsReservedAndKeepsIds()
    {
        var entries = WledJsonParsers.ParseNameArray("""["Solid","RSVD","Ripple","-","Fire"]""");
        Assert.Equal(3, entries.Count);
        Assert.Equal(0, entries[0].Id);
        Assert.Equal("Solid", entries[0].Name);
        Assert.Equal(2, entries[1].Id);
        Assert.Equal("Ripple", entries[1].Name);
        Assert.Equal(4, entries[2].Id);
    }

    [Fact]
    public void ParseState_ReadsSegmentFxPaletteOptionsAndColors()
    {
        var state = WledJsonParsers.ParseState(
            """
            {
              "on": true,
              "bri": 200,
              "ps": 3,
              "pl": -1,
              "live": false,
              "mainseg": 0,
              "seg": [{
                "id": 0,
                "start": 0,
                "stop": 60,
                "fx": 79,
                "sx": 38,
                "ix": 38,
                "pal": 62,
                "o1": true,
                "o2": false,
                "o3": true,
                "col": [[71,255,153],[0,0,0],[255,255,255]]
              }]
            }
            """);

        Assert.True(state.On);
        Assert.Equal(200, state.Brightness);
        Assert.Equal(3, state.PresetId);
        Assert.Equal(79, state.MainSegment.FxId);
        Assert.Equal(62, state.MainSegment.PaletteId);
        Assert.True(state.MainSegment.Overlay);
        Assert.True(state.MainSegment.Option3);
        Assert.Equal(71, state.MainSegment.Primary.R);
        Assert.Equal(255, state.MainSegment.Primary.G);
    }

    [Fact]
    public void ParsePresets_ReadsNamedSlots()
    {
        var presets = WledJsonParsers.ParsePresets(
            """
            {
              "0": {},
              "1": { "n": "Bay warm" },
              "5": { "n": "Celebrate" }
            }
            """);

        Assert.Equal(2, presets.Count);
        Assert.Equal(1, presets[0].Id);
        Assert.Equal("Bay warm", presets[0].Name);
        Assert.Equal(5, presets[1].Id);
    }

    [Fact]
    public void BuildPatchBody_IncludesSegmentControls()
    {
        var body = WledJsonParsers.BuildPatchBody(new WledStatePatch
        {
            On = true,
            Brightness = 180,
            Live = false,
            SegmentId = 0,
            FxId = 79,
            Speed = 38,
            Intensity = 38,
            PaletteId = 62,
            Overlay = true,
            Primary = RgbColor.FromRgb(255, 0, 0),
            Secondary = RgbColor.FromRgb(0, 0, 0),
            Tertiary = RgbColor.FromRgb(255, 255, 255)
        });
        var json = JsonSerializer.Serialize(body);
        using var doc = JsonDocument.Parse(json);
        var seg = doc.RootElement.GetProperty("seg")[0];
        Assert.Equal(79, seg.GetProperty("fx").GetInt32());
        Assert.Equal(62, seg.GetProperty("pal").GetInt32());
        Assert.True(seg.GetProperty("o1").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("live").GetBoolean());
    }

    [Fact]
    public void AmbientFactory_MapsRippleRedReefLayeredTimingAndMaxColors()
    {
        var patch = WledAmbientStateFactory.CreateRippleAmbientPatch(brightness: 210);
        Assert.Equal(EffectConfig.RippleFxId, patch.FxId);
        Assert.Equal(EffectConfig.RedReefPaletteId, patch.PaletteId);
        Assert.Equal(EffectConfig.RippleTimingByte, patch.Speed);
        Assert.Equal(EffectConfig.RippleTimingByte, patch.Intensity);
        Assert.True(patch.Overlay);
        Assert.False(patch.Live);
        Assert.Equal((byte)210, patch.Brightness!.Value);
        Assert.Equal((byte)255, patch.Primary!.G);
        Assert.Equal((byte)71, patch.Primary.R);
        Assert.Equal((byte)153, patch.Primary.B);
    }
}
