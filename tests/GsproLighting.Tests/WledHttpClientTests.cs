using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpClientTests
{
    [Fact]
    public async Task ApplyPresetAsync_RetriesOnceOn413_ThenSucceeds()
    {
        var handler = new SequencedStatusHandler([HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.OK]);
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        await http.ApplyPresetAsync("192.168.1.50", new WledPresetRequest { FxId = 79 });

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ApplyPresetAsync_413Twice_ThrowsFriendlyMessage()
    {
        var handler = new SequencedStatusHandler(
            [HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestEntityTooLarge]);
        using var http = new WledHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => http.ApplyPresetAsync("192.168.1.50", new WledPresetRequest { FxId = 79 }));

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
    public void BuildStateBody_RippleAmbient_IncludesFxSxIxPalOverlayAndColors()
    {
        var slot = EffectConfig.CreateRippleAmbient(RgbColor.FromRgb(61, 220, 132));
        var request = WledPresetRequest.FromSlot(slot, brightness: 200);
        var json = JsonSerializer.Serialize(WledHttpClient.BuildStateBody(request));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(200, root.GetProperty("bri").GetInt32());

        var seg = root.GetProperty("seg")[0];
        Assert.Equal(79, seg.GetProperty("fx").GetInt32());
        Assert.Equal(38, seg.GetProperty("sx").GetInt32());
        Assert.Equal(38, seg.GetProperty("ix").GetInt32());
        Assert.Equal(62, seg.GetProperty("pal").GetInt32());
        Assert.True(seg.GetProperty("o1").GetBoolean());

        var primary = seg.GetProperty("col")[0];
        Assert.Equal(71, primary[0].GetInt32());
        Assert.Equal(255, primary[1].GetInt32());
        Assert.Equal(153, primary[2].GetInt32());
    }

    [Fact]
    public void BuildStateBody_FxOnly_OmitsOptionalSegmentFields()
    {
        var json = JsonSerializer.Serialize(
            WledHttpClient.BuildStateBody(new WledPresetRequest { FxId = 89 }));
        using var doc = JsonDocument.Parse(json);
        var seg = doc.RootElement.GetProperty("seg")[0];

        Assert.Equal(89, seg.GetProperty("fx").GetInt32());
        Assert.False(seg.TryGetProperty("sx", out _));
        Assert.False(seg.TryGetProperty("ix", out _));
        Assert.False(seg.TryGetProperty("pal", out _));
        Assert.False(seg.TryGetProperty("o1", out _));
        Assert.False(seg.TryGetProperty("col", out _));
    }

    [Fact]
    public void FromSlot_MapsWledOptionsAndMaxColors()
    {
        var slot = EffectConfig.CreateRippleAmbient(RgbColor.FromRgb(212, 160, 23));
        var request = WledPresetRequest.FromSlot(slot);

        Assert.Equal(79, request.FxId);
        Assert.Equal(38, request.Speed);
        Assert.Equal(38, request.Intensity);
        Assert.Equal(62, request.PaletteId);
        Assert.True(request.Overlay);
        Assert.Equal(255, request.Primary!.R);
        Assert.Equal(192, request.Primary.G);
        Assert.Equal(28, request.Primary.B);
    }
}
