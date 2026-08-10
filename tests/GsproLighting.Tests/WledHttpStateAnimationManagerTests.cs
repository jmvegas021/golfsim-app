using System.Net;
using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpStateAnimationManagerTests
{
    [Fact]
    public async Task NewSolidAction_CancelsBreathingBeforeItWrites()
    {
        var handler = new BlockingFirstRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var breathing = manager.RunNotReadyAsync("192.168.86.40", ledCount: 8, brightness: 180);
        await handler.FirstRequestStarted;
        var solid = manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 0, 255),
            180);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breathing);
        await solid;

        Assert.Equal(1, handler.MaximumConcurrentRequests);
        Assert.Contains("[0,0,255]", handler.Bodies[^1]);
    }

    [Fact]
    public async Task CancelActive_StopsContinuousBreathingWithoutAnotherFrame()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var breathing = manager.RunNotReadyAsync("192.168.86.40", ledCount: 8, brightness: 180);
        await handler.FirstRequestCompleted;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breathing);
        var countAfterCancellation = handler.Bodies.Count;

        Assert.Equal(countAfterCancellation, handler.Bodies.Count);
    }

    [Fact]
    public async Task RunReadyAsync_FromKnownRed_MorphsTowardGreenThenChasesToCenter()
    {
        var chaseCount = WledHttpReadyAnimationBuilder.CreateReadyChaseFromFullSequence(12, 180).Count;
        var expectedTotal = 1 + WledHttpAnimationFrameFactory.ColorTransitionStepCount + chaseCount;
        var handler = new RecordingHandler(completeAfterCount: expectedTotal);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(180, 30, 30),
            180);
        await manager.RunReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Equal(expectedTotal, handler.Bodies.Count);
        var morphBodies = handler.Bodies.Skip(1)
            .Take(WledHttpAnimationFrameFactory.ColorTransitionStepCount)
            .ToArray();
        Assert.All(morphBodies, body =>
        {
            using var doc = JsonDocument.Parse(body);
            var segments = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            Assert.Equal(3, segments.Length);
            Assert.Equal(0, segments[0].GetProperty("start").GetInt32());
            Assert.Equal(12, segments[0].GetProperty("stop").GetInt32());
            Assert.Equal(0, segments[1].GetProperty("stop").GetInt32());
            Assert.True(doc.RootElement.GetProperty("bri").GetInt32() > 0);
        });

        using var firstMorph = JsonDocument.Parse(morphBodies[0]);
        using var lastMorph = JsonDocument.Parse(morphBodies[^1]);
        var firstColor = ReadPrimaryColor(firstMorph.RootElement.GetProperty("seg")[0]);
        var lastColor = ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]);
        Assert.True(firstColor[1] < lastColor[1]);
        Assert.Equal([0, 220, 0], lastColor);

        using var hold = JsonDocument.Parse(handler.Bodies[^1]);
        var holdSegments = hold.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(3, holdSegments.Length);
        var holdLit = holdSegments[1].GetProperty("stop").GetInt32() -
            holdSegments[1].GetProperty("start").GetInt32();
        Assert.Equal(WledHttpReadyAnimationBuilder.ResolveHoldLitCount(12), holdLit);
    }

    [Fact]
    public async Task RunNotReadyAsync_FromKnownGreen_MorphsFullStripRedThenBreathes()
    {
        var morphCount = WledHttpAnimationFrameFactory.ColorTransitionStepCount;
        var handler = new RecordingHandler(completeAfterCount: 1 + morphCount + 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 220, 0),
            180);
        var notReady = manager.RunNotReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);
        await handler.Completed;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

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

        using var firstBreathe = JsonDocument.Parse(handler.Bodies[1 + morphCount]);
        var breatheSegs = firstBreathe.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, breatheSegs[0].GetProperty("start").GetInt32());
        Assert.Equal(12, breatheSegs[0].GetProperty("stop").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(breatheSegs[0]));
        Assert.DoesNotContain("[0,220,0]", handler.Bodies.Skip(1));
    }

    [Fact]
    public async Task RunNotReadyAsync_AfterReadyHold_OverwritesGreenCenterWithFullStripRed()
    {
        const int ledCount = 12;
        var readyCount = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, 180).Count;
        var morphCount = WledHttpAnimationFrameFactory.ColorTransitionStepCount;
        var handler = new RecordingHandler(completeAfterCount: readyCount + morphCount + 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.RunReadyAsync("192.168.86.40", ledCount, brightness: 180);
        var notReady = manager.RunNotReadyAsync("192.168.86.40", ledCount, brightness: 180);
        await handler.Completed;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

        using var readyHold = JsonDocument.Parse(handler.Bodies[readyCount - 1]);
        var holdSegs = readyHold.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(3, holdSegs.Length);
        Assert.Equal([0, 220, 0], ReadPrimaryColor(holdSegs[1]));

        var afterReady = handler.Bodies.Skip(readyCount).ToArray();
        Assert.All(afterReady, body =>
        {
            Assert.DoesNotContain("[0,220,0]", body);
            using var doc = JsonDocument.Parse(body);
            var segs = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            // Full-strip overwrite clears Ready's green center-band geometry.
            Assert.Equal(0, segs[0].GetProperty("start").GetInt32());
            Assert.Equal(ledCount, segs[0].GetProperty("stop").GetInt32());
            Assert.Equal(0, segs[1].GetProperty("stop").GetInt32());
            Assert.Equal(0, segs[2].GetProperty("stop").GetInt32());
        });

        using var lastMorph = JsonDocument.Parse(afterReady[morphCount - 1]);
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]));
        using var firstBreathe = JsonDocument.Parse(afterReady[morphCount]);
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(firstBreathe.RootElement.GetProperty("seg")[0]));
    }

    [Fact]
    public async Task RunNotReadyAsync_FromOff_UsesCenterOutThenBreathesFromFullBrightness()
    {
        const int ledCount = 8;
        var expandCount = WledHttpAnimationFrameFactory.ResolveCenterOutStepCount(ledCount) + 1;
        var handler = new RecordingHandler(completeAfterCount: expandCount + 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var notReady = manager.RunNotReadyAsync("192.168.86.40", ledCount, brightness: 200);
        await handler.Completed;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

        using var firstExpand = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal(3, firstExpand.RootElement.GetProperty("seg").GetArrayLength());

        using var hold = JsonDocument.Parse(handler.Bodies[expandCount - 1]);
        var holdSegs = hold.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, holdSegs[0].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, holdSegs[0].GetProperty("stop").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(holdSegs[0]));

        using var firstBreathe = JsonDocument.Parse(handler.Bodies[expandCount]);
        Assert.Equal(200, firstBreathe.RootElement.GetProperty("bri").GetInt32());
        var breatheSegs = firstBreathe.RootElement.GetProperty("seg").EnumerateArray().ToArray();
        Assert.Equal(0, breatheSegs[0].GetProperty("start").GetInt32());
        Assert.Equal(ledCount, breatheSegs[0].GetProperty("stop").GetInt32());
        Assert.Equal([180, 30, 30], ReadPrimaryColor(breatheSegs[0]));
        Assert.Equal(0, breatheSegs[1].GetProperty("stop").GetInt32());
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
