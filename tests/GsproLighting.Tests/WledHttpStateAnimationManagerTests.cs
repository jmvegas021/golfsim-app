using System.Net;
using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpStateAnimationManagerTests
{
    [Fact]
    public async Task RunReadyAsync_IsQuarantined_DoesNotPost()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

#pragma warning disable CS0618
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.RunReadyAsync("192.168.86.40", ledCount: 8, brightness: 180));
#pragma warning restore CS0618

        Assert.Contains("quarantined", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task RunNotReadyAsync_IsQuarantined_DoesNotPost()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

#pragma warning disable CS0618
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.RunNotReadyAsync("192.168.86.40", ledCount: 8, brightness: 180));
#pragma warning restore CS0618

        Assert.Contains("DDP", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task RunHitDirectionAsync_IsQuarantined_DoesNotPost()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

#pragma warning disable CS0618
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.RunHitDirectionAsync(
                "192.168.86.40",
                ShotDirection.Left,
                ledCount: 8,
                brightness: 180));
#pragma warning restore CS0618

        Assert.Contains("quarantined", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task NewSolidAction_CancelsPriorSolidBeforeItWrites()
    {
        var handler = new BlockingFirstRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var first = manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(255, 0, 0),
            180,
            ledCount: 8);
        await handler.FirstRequestStarted;
        var second = manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 0, 255),
            180,
            ledCount: 8);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await second;

        Assert.Equal(1, handler.MaximumConcurrentRequests);
        Assert.Contains("[0,0,255]", handler.Bodies[^1]);
    }

    [Fact]
    public async Task CancelActive_StopsSolidWithoutAnotherFrame()
    {
        var handler = new BlockingFirstRequestHandler();
        using var http = new HttpClient(handler);
        using var client = new WledDeviceClient(http);
        using var manager = new WledHttpStateAnimationManager(client);

        var solid = manager.ApplySolidAsync(
            "192.168.86.40",
            RgbColor.FromRgb(0, 255, 0),
            180,
            ledCount: 8);
        await handler.FirstRequestStarted;
        manager.CancelActive();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => solid);
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
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return Ok();
        }
    }

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent("{}") };
}
