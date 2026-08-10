using System.Net;
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
        private readonly TaskCompletionSource _firstRequestCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstRequestCompleted => _firstRequestCompleted.Task;
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            _firstRequestCompleted.TrySetResult();
            return Ok();
        }
    }

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent("{}") };
}
