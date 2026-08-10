using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledSolidHttpApplierTests
{
    [Fact]
    public void CreateSolidBody_IsAuthoritativeFx0FullStrip()
    {
        var color = RgbColor.FromRgb(255, 40, 10);
        var body = WledSolidHttpApplier.CreateSolidBody(ledCount: 90, color, brightness: 200);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;
        var seg0 = root.GetProperty("seg")[0];

        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.Equal(200, root.GetProperty("bri").GetInt32());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.Equal(0, root.GetProperty("mainseg").GetInt32());
        Assert.Equal(0, seg0.GetProperty("fx").GetInt32());
        Assert.Equal(0, seg0.GetProperty("pal").GetInt32());
        Assert.Equal(90, seg0.GetProperty("stop").GetInt32());
        Assert.Equal(
            [255, 40, 10],
            seg0.GetProperty("col")[0].EnumerateArray().Select(v => v.GetInt32()).ToArray());
        Assert.Equal(0, root.GetProperty("seg")[1].GetProperty("stop").GetInt32());
    }

    [Fact]
    public void CreateOffBody_IsOff_LiveFalse_StopsPresets()
    {
        var body = WledSolidHttpApplier.CreateOffBody();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
        var root = doc.RootElement;

        Assert.False(root.GetProperty("on").GetBoolean());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.False(root.TryGetProperty("seg", out _));
    }

    [Fact]
    public async Task ApplySolidAsync_PostsAuthoritativeSolidBodyToJsonState()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var applier = new WledSolidHttpApplier(client);

        await applier.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 255, 0),
            brightness: 180,
            ledCount: 48);

        Assert.Equal(1, handler.PostCount);
        Assert.Equal("/json/state", handler.LastPath);
        Assert.Equal("192.168.86.40", handler.LastHost);
        Assert.Contains("\"fx\":0", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        Assert.Contains("\"ps\":-1", handler.LastBody);
        Assert.Contains("\"pl\":-1", handler.LastBody);
        Assert.Contains("\"mainseg\":0", handler.LastBody);
        Assert.Contains("\"stop\":48", handler.LastBody);
        Assert.Contains("[0,255,0]", handler.LastBody);
    }

    [Fact]
    public async Task ApplyOffAsync_PostsOffLiveFalseAndStopsPresets()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var applier = new WledSolidHttpApplier(client);

        await applier.ApplyOffAsync("192.168.86.40");

        Assert.Equal(1, handler.PostCount);
        Assert.Contains("\"on\":false", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        Assert.Contains("\"ps\":-1", handler.LastBody);
        Assert.Contains("\"pl\":-1", handler.LastBody);
        Assert.DoesNotContain("\"seg\"", handler.LastBody);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int PostCount { get; private set; }
        public string LastBody { get; private set; } = "";
        public string? LastHost { get; private set; }
        public string? LastPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            PostCount++;
            LastHost = request.RequestUri?.Host;
            LastPath = request.RequestUri?.AbsolutePath;
            LastBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
