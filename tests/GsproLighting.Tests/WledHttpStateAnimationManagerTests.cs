using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpStateAnimationManagerTests
{
    [Fact]
    public async Task NewSolidAction_CancelsInFlightNotReadyBeforeItWrites()
    {
        var handler = new BlockingFirstRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var notReady = manager.RunNotReadyAsync("192.168.86.40", ledCount: 8, brightness: 180);
        await handler.FirstRequestStarted;
        var solid = manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 0, 255),
            180);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);
        await solid;

        Assert.Equal(1, handler.MaximumConcurrentRequests);
        Assert.Contains("[0,0,255]", handler.Bodies[^1]);
    }

    [Fact]
    public async Task RunReadyAsync_PostsSingleChaseAuroraFullStrip()
    {
        var handler = new RecordingHandler(completeAfterCount: 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Single(handler.Bodies);
        using var doc = JsonDocument.Parse(handler.Bodies[0]);
        var segs = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(EffectConfig.ChaseFxId, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(EffectConfig.AuroraPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("sx").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("ix").GetInt32());
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(12, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[2].GetProperty("stop").GetInt32());
    }

    [Fact]
    public async Task RunNotReadyAsync_PostsSingleRedChaseFullStrip_NotAurora()
    {
        var handler = new RecordingHandler(completeAfterCount: 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunNotReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Single(handler.Bodies);
        using var doc = JsonDocument.Parse(handler.Bodies[0]);
        var segs = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(EffectConfig.ChaseFxId, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(0, segs[0].GetProperty("pal").GetInt32());
        Assert.NotEqual(EffectConfig.AuroraPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("sx").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("ix").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(segs[0]));
        Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[2].GetProperty("stop").GetInt32());
    }

    [Fact]
    public async Task RunNotReadyAsync_AfterReady_ClearsSegmentsAndUsesRedNotAurora()
    {
        var handler = new RecordingHandler(completeAfterCount: 2);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);
        await manager.RunNotReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Equal(2, handler.Bodies.Count);
        using var ready = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal(
            EffectConfig.AuroraPaletteId,
            ready.RootElement.GetProperty("seg")[0].GetProperty("pal").GetInt32());

        using var notReady = JsonDocument.Parse(handler.Bodies[1]);
        var segs = notReady.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(segs[0]));
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(12, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
        Assert.Equal(0, segs[2].GetProperty("stop").GetInt32());
        Assert.DoesNotContain("\"pal\":50", handler.Bodies[1]);
    }

    private sealed class BlockingFirstRequestHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _firstRequestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeRequests;

        public Task FirstRequestStarted => _firstRequestStarted.Task;
        public int MaximumConcurrentRequests { get; private set; }
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeRequests);
            MaximumConcurrentRequests = Math.Max(MaximumConcurrentRequests, active);
            try
            {
                Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                if (Bodies.Count == 1)
                {
                    _firstRequestStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return Ok();
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
            }
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly int _completeAfterCount;
        private readonly TaskCompletionSource _completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingHandler(int completeAfterCount = 1) =>
            _completeAfterCount = completeAfterCount;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (Bodies.Count >= _completeAfterCount)
                _completed.TrySetResult();
            return Ok();
        }
    }

    private static int[] ReadPrimaryColor(JsonElement segment) =>
        segment.GetProperty("col")[0].EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent("{}") };
}
