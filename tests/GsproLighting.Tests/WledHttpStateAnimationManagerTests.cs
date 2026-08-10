using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpStateAnimationManagerTests
{
    [Fact]
    public async Task NewSolidAction_CancelsNotReadyBeforeItWrites()
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
            180,
            ledCount: 8);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);
        await solid;

        Assert.Equal(1, handler.MaximumConcurrentRequests);
        Assert.Contains("[0,0,255]", handler.Bodies[^1]);
    }

    [Fact]
    public async Task CancelActive_StopsReadyIntroWithoutAnotherFrame()
    {
        var handler = new BlockingFirstRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var ready = manager.RunReadyAsync("192.168.86.40", ledCount: 8, brightness: 180);
        await handler.FirstRequestStarted;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
        var countAfterCancellation = handler.Bodies.Count;

        Assert.Equal(countAfterCancellation, handler.Bodies.Count);
    }

    [Fact]
    public async Task RunReadyAsync_FromKnownRed_MorphsThenConcentratesToSolidCenterBand()
    {
        var concentrateCount = WledHttpReadyAnimationBuilder.CreateReadyChaseFromFullSequence(12, 180).Count;
        var expectedTotal = 1 + WledHttpAnimationFrameFactory.ColorTransitionStepCount + concentrateCount;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(180, 30, 30),
            180,
            ledCount: 12);
        await manager.RunReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        var morphBodies = handler.Bodies.Skip(1)
            .Take(WledHttpAnimationFrameFactory.ColorTransitionStepCount)
            .ToArray();
        Assert.All(morphBodies, body =>
        {
            using var doc = JsonDocument.Parse(body);
            var segments = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            Assert.Equal(0, segments[0].GetProperty("start").GetInt32());
            Assert.Equal(12, segments[0].GetProperty("stop").GetInt32());
            Assert.Equal(0, segments[1].GetProperty("stop").GetInt32());
            Assert.True(doc.RootElement.GetProperty("bri").GetInt32() > 0);
            Assert.Equal(-1, doc.RootElement.GetProperty("ps").GetInt32());
            Assert.Equal(-1, doc.RootElement.GetProperty("pl").GetInt32());
        });

        using var hold = JsonDocument.Parse(handler.Bodies[^1]);
        AssertReadyCenterBandSolidHold(hold.RootElement, ledCount: 12);
    }

    [Fact]
    public async Task RunNotReadyAsync_FromKnownGreen_MorphsThenHalfExpandThenRedChase()
    {
        var morphCount = WledHttpAnimationFrameFactory.ColorTransitionStepCount;
        var expandCount = WledHttpAnimationFrameFactory
            .CreateNotReadyExpandFromHalfSequence(12, 180)
            .Count;
        var expectedTotal = 1 + morphCount + expandCount + 1;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 220, 0),
            180,
            ledCount: 12);
        await manager.RunNotReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        var morphBodies = handler.Bodies.Skip(1).Take(morphCount).ToArray();
        Assert.Equal(morphCount, morphBodies.Length);
        Assert.All(morphBodies, body =>
        {
            using var doc = JsonDocument.Parse(body);
            var segments = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            Assert.Equal(0, segments[0].GetProperty("start").GetInt32());
            Assert.Equal(12, segments[0].GetProperty("stop").GetInt32());
            Assert.Equal(0, segments[1].GetProperty("stop").GetInt32());
            Assert.Equal(0, segments[2].GetProperty("stop").GetInt32());
        });

        using var lastMorph = JsonDocument.Parse(morphBodies[^1]);
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]));

        var halfStartIndex = 1 + morphCount;
        using var halfStart = JsonDocument.Parse(handler.Bodies[halfStartIndex]);
        var halfSegs = halfStart.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        var concentrate = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(12);
        Assert.Equal(
            concentrate,
            halfSegs[1].GetProperty("stop").GetInt32() - halfSegs[1].GetProperty("start").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(halfSegs[1]));

        using var chaseHold = JsonDocument.Parse(handler.Bodies[^1]);
        AssertNotReadyFullStripHold(chaseHold.RootElement, ledCount: 12);
        Assert.DoesNotContain($"\"pal\":{EffectConfig.AuroraPaletteId}", handler.Bodies[^1]);
        Assert.DoesNotContain("[0,220,0]", handler.Bodies.Skip(1));
    }

    [Fact]
    public async Task RunNotReadyAsync_AfterReady_OverwritesGreenWithFullStripRedMorph()
    {
        const int ledCount = 12;
        var readyCount = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, 180).Count;
        var morphCount = WledHttpAnimationFrameFactory.ColorTransitionStepCount;
        var expandCount = WledHttpAnimationFrameFactory
            .CreateNotReadyExpandFromHalfSequence(ledCount, 180)
            .Count;
        var expectedTotal = readyCount + morphCount + expandCount + 1;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunReadyAsync("192.168.86.40", ledCount, brightness: 180);
        await manager.RunNotReadyAsync("192.168.86.40", ledCount, brightness: 180);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        using var readyHold = JsonDocument.Parse(handler.Bodies[readyCount - 1]);
        AssertReadyCenterBandSolidHold(readyHold.RootElement, ledCount);

        var morphBodies = handler.Bodies.Skip(readyCount).Take(morphCount).ToArray();
        Assert.All(morphBodies, body =>
        {
            Assert.DoesNotContain("[0,220,0]", body);
            using var doc = JsonDocument.Parse(body);
            var segs = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
            Assert.Equal(ledCount, segs[0].GetProperty("stop").GetInt32());
            Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
            Assert.Equal(-1, doc.RootElement.GetProperty("ps").GetInt32());
        });

        using var lastMorph = JsonDocument.Parse(morphBodies[^1]);
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]));

        using var notReadyHold = JsonDocument.Parse(handler.Bodies[^1]);
        AssertNotReadyFullStripHold(notReadyHold.RootElement, ledCount);
    }

    [Fact]
    public async Task RunNotReadyAsync_FromOff_UsesCenterOutThenRedChase()
    {
        const int ledCount = 8;
        var expandCount = WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount) + 1;
        var expectedTotal = expandCount + 1;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunNotReadyAsync("192.168.86.40", ledCount, brightness: 200);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        using var firstExpand = JsonDocument.Parse(handler.Bodies[0]);
        var firstSegs = firstExpand.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.True(firstSegs.Length >= 3);
        Assert.Equal(0, firstSegs[0].GetProperty("fx").GetInt32());
        Assert.Equal(-1, firstExpand.RootElement.GetProperty("ps").GetInt32());
        Assert.Equal(-1, firstExpand.RootElement.GetProperty("pl").GetInt32());

        using var hold = JsonDocument.Parse(handler.Bodies[expandCount - 1]);
        var holdSegs = hold.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, holdSegs[0].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, holdSegs[0].GetProperty("stop").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(holdSegs[0]));

        using var chase = JsonDocument.Parse(handler.Bodies[^1]);
        AssertNotReadyFullStripHold(chase.RootElement, ledCount);
        Assert.Equal(200, chase.RootElement.GetProperty("bri").GetInt32());
    }

    [Fact]
    public async Task RunReadyAsync_FromOff_EndsSolidCenterBandGreen()
    {
        const int ledCount = 12;
        var readyCount = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, 180).Count;
        var expectedTotal = readyCount;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunReadyAsync("192.168.86.40", ledCount, brightness: 180);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        using var first = JsonDocument.Parse(handler.Bodies[0]);
        var firstSegs = first.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, firstSegs[0].GetProperty("fx").GetInt32());
        Assert.Equal(0, firstSegs[0].GetProperty("start").GetInt32());
        Assert.Equal(1, firstSegs[0].GetProperty("stop").GetInt32());

        using var last = JsonDocument.Parse(handler.Bodies[^1]);
        AssertReadyCenterBandSolidHold(last.RootElement, ledCount);
        Assert.DoesNotContain($"\"fx\":{EffectConfig.ChaseFxId}", handler.Bodies[^1]);
        Assert.DoesNotContain($"\"pal\":{EffectConfig.AuroraPaletteId}", handler.Bodies[^1]);
    }

    private static void AssertReadyCenterBandSolidHold(JsonElement root, int ledCount)
    {
        Assert.True(root.GetProperty("on").GetBoolean());
        Assert.False(root.GetProperty("live").GetBoolean());
        Assert.Equal(0, root.GetProperty("tt").GetInt32());
        Assert.Equal(-1, root.GetProperty("ps").GetInt32());
        Assert.Equal(-1, root.GetProperty("pl").GetInt32());
        Assert.Equal(0, root.GetProperty("mainseg").GetInt32());

        var concentrateLit = WledHttpReadyAnimationBuilder.ResolveConcentrateLitCount(ledCount);
        var segs = root.GetProperty("seg").EnumerateArray().ToArray();
        Assert.True(segs.Length >= 3);
        Assert.Equal(0, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(0, segs[1].GetProperty("fx").GetInt32());
        Assert.Equal(
            concentrateLit,
            segs[1].GetProperty("stop").GetInt32() - segs[1].GetProperty("start").GetInt32());
        Assert.Equal([0, 220, 0], ReadPrimaryColor(segs[1]));
        Assert.Equal([0, 0, 0], ReadPrimaryColor(segs[0]));
        Assert.Equal([0, 0, 0], ReadPrimaryColor(segs[2]));
    }

    private static void AssertNotReadyFullStripHold(JsonElement root, int ledCount)
    {
        Assert.Equal(0, root.GetProperty("tt").GetInt32());
        var segs = root.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, segs[0].GetProperty("stop").GetInt32());
        Assert.Equal(EffectConfig.ChaseFxId, segs[0].GetProperty("fx").GetInt32());
        Assert.Equal(EffectConfig.RedReefPaletteId, segs[0].GetProperty("pal").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("sx").GetInt32());
        Assert.Equal(255, segs[0].GetProperty("ix").GetInt32());
        Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
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
        private readonly TaskCompletionSource _firstRequestCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingHandler(int completeAfterCount = 1) =>
            _completeAfterCount = completeAfterCount;

        public Task FirstRequestCompleted => _firstRequestCompleted.Task;
        public Task Completed => _completed.Task;
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            _firstRequestCompleted.TrySetResult();
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
