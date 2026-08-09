using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledConnectionSnapshotReaderTests
{
    [Fact]
    public async Task ReadAsync_MapsInfoLedCountAndStateBrightness()
    {
        var handler = new MapHandler(
            ("/json/info", """{"name":"Bay","ver":"0.14.0","leds":{"count":150,"maxseg":1},"fxcount":1,"palcount":1,"mac":"aa"}"""),
            ("/json/state", """{"on":true,"bri":200,"ps":-1,"pl":-1,"live":false,"mainseg":0,"seg":[{"id":0,"start":0,"stop":150,"fx":0,"sx":128,"ix":128,"pal":0,"col":[[255,0,0],[0,0,0],[0,0,0]],"o1":false,"o2":false,"o3":false}]}"""));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var reader = new WledConnectionSnapshotReader(client);

        var snapshot = await reader.ReadAsync("192.168.86.89");

        Assert.Equal(150, snapshot.LedCount);
        Assert.Equal(200, snapshot.Brightness);
        Assert.Equal("Bay", snapshot.DeviceName);
        Assert.Equal("0.14.0", snapshot.Version);
    }

    [Fact]
    public async Task ReadAsync_FloorsZeroBrightnessToOne()
    {
        var handler = new MapHandler(
            ("/json/info", """{"name":"Bay","ver":"0.14.0","leds":{"count":60,"maxseg":1},"fxcount":1,"palcount":1,"mac":"aa"}"""),
            ("/json/state", """{"on":false,"bri":0,"ps":-1,"pl":-1,"live":false,"mainseg":0,"seg":[]}"""));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        using var client = new WledDeviceClient(http);
        using var reader = new WledConnectionSnapshotReader(client);

        var snapshot = await reader.ReadAsync("192.168.86.89");

        Assert.Equal(1, snapshot.Brightness);
    }

    private sealed class MapHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _bodies;

        public MapHandler(params (string Path, string Body)[] responses) =>
            _bodies = responses.ToDictionary(r => r.Path, r => r.Body, StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = _bodies.TryGetValue(path, out var json) ? json : "{}";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}
