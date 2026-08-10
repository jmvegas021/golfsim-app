using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledSolidHttpApplierTests
{
    [Fact]
    public void CreateSolidPatch_IsFx0_LiveFalse_WithRgb()
    {
        var color = RgbColor.FromRgb(255, 40, 10);
        var patch = WledSolidHttpApplier.CreateSolidPatch(color, brightness: 200);

        Assert.True(patch.On);
        Assert.Equal((byte)200, patch.Brightness);
        Assert.False(patch.Live);
        Assert.Equal(0, patch.FxId);
        Assert.Equal(0, patch.SegmentId);
        Assert.Equal(255, patch.Primary!.R);
        Assert.Equal(40, patch.Primary.G);
        Assert.Equal(10, patch.Primary.B);

        var json = JsonSerializer.Serialize(WledJsonParsers.BuildPatchBody(patch));
        Assert.Contains("\"fx\":0", json);
        Assert.Contains("\"live\":false", json);
        Assert.Contains("\"on\":true", json);
        Assert.Contains("\"bri\":200", json);
        Assert.Contains("[255,40,10]", json);
        Assert.Contains("[0,0,0]", json);
    }

    [Fact]
    public void CreateOffPatch_IsOff_LiveFalse_NoSegment()
    {
        var patch = WledSolidHttpApplier.CreateOffPatch();
        Assert.False(patch.On);
        Assert.False(patch.Live);
        Assert.Null(patch.FxId);

        var body = (Dictionary<string, object?>)WledJsonParsers.BuildPatchBody(patch);
        Assert.False(body.ContainsKey("seg"));
        Assert.Equal(false, body["on"]);
        Assert.Equal(false, body["live"]);
    }

    [Fact]
    public async Task ApplySolidAsync_PostsSolidBodyToJsonState()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var applier = new WledSolidHttpApplier(client);

        await applier.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 255, 0),
            brightness: 180);

        Assert.Equal(1, handler.PostCount);
        Assert.Equal("/json/state", handler.LastPath);
        Assert.Equal("192.168.86.40", handler.LastHost);
        Assert.Contains("\"fx\":0", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
        Assert.Contains("[0,255,0]", handler.LastBody);
    }

    [Fact]
    public async Task ApplyOffAsync_PostsOffLiveFalse()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var applier = new WledSolidHttpApplier(client);

        await applier.ApplyOffAsync("192.168.86.40");

        Assert.Equal(1, handler.PostCount);
        Assert.Contains("\"on\":false", handler.LastBody);
        Assert.Contains("\"live\":false", handler.LastBody);
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
