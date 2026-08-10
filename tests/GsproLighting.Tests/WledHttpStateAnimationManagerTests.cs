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
    public async Task RunReadyAsync_FromKnownRed_TransitionsTowardGreenWithoutCenterOutBlack()
    {
        var handler = new RecordingHandler(completeAfterCount: 1 + WledHttpAnimationFrameFactory.ColorTransitionStepCount);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        await manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(180, 30, 30),
            180);
        await manager.RunReadyAsync("192.168.86.40", ledCount: 12, brightness: 180);

        Assert.Equal(1 + WledHttpAnimationFrameFactory.ColorTransitionStepCount, handler.Bodies.Count);
        var morphBodies = handler.Bodies.Skip(1).ToArray();
        Assert.All(morphBodies, body =>
        {
            using var doc = JsonDocument.Parse(body);
            var segments = doc.RootElement.GetProperty("seg").EnumerateArray().ToArray();
            Assert.Single(segments);
            Assert.False(segments[0].TryGetProperty("start", out _));
            Assert.True(doc.RootElement.GetProperty("bri").GetInt32() > 0);
        });

        using var firstMorph = JsonDocument.Parse(morphBodies[0]);
        using var lastMorph = JsonDocument.Parse(morphBodies[^1]);
        var firstColor = ReadPrimaryColor(firstMorph.RootElement.GetProperty("seg")[0]);
        var lastColor = ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]);
        Assert.True(firstColor[1] < lastColor[1]);
        Assert.Equal([0, 220, 0], lastColor);
    }

    [Fact]
    public async Task RunNotReadyAsync_FromKnownGreen_MorphsToRedInsteadOfCenterOutExpand()
    {
        var handler = new RecordingHandler(
            completeAfterCount: 1 + WledHttpAnimationFrameFactory.ColorTransitionStepCount);
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

        var morphBodies = handler.Bodies.Skip(1).Take(WledHttpAnimationFrameFactory.ColorTransitionStepCount)
            .ToArray();
        Assert.Equal(WledHttpAnimationFrameFactory.ColorTransitionStepCount, morphBodies.Length);
        Assert.All(morphBodies, body =>
        {
            using var doc = JsonDocument.Parse(body);
            Assert.Single(doc.RootElement.GetProperty("seg").EnumerateArray());
        });

        using var lastMorph = JsonDocument.Parse(morphBodies[^1]);
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(lastMorph.RootElement.GetProperty("seg")[0]));
    }

    [Fact]
    public async Task RunNotReadyAsync_FromOff_UsesCenterOutThenBreathesFromFullBrightness()
    {
        var expandCount = 8 + 1;
        var handler = new RecordingHandler(completeAfterCount: expandCount + 1);
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var notReady = manager.RunNotReadyAsync("192.168.86.40", ledCount: 8, brightness: 200);
        await handler.Completed;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => notReady);

        using var firstExpand = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal(3, firstExpand.RootElement.GetProperty("seg").GetArrayLength());

        using var firstBreathe = JsonDocument.Parse(handler.Bodies[expandCount]);
        Assert.Equal(200, firstBreathe.RootElement.GetProperty("bri").GetInt32());
        Assert.Equal(
            [180, 30, 30],
            ReadPrimaryColor(firstBreathe.RootElement.GetProperty("seg")[0]));
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
