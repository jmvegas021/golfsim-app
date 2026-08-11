using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Gspro.Dispatch;
using Xunit;

namespace GsproLighting.Tests;

public sealed class OpenConnectReadyEdgeDispatcherTests
{
    [Fact]
    public void ReadyHeartbeats_FireOnceUntilShotClearsEdge()
    {
        var sink = new RecordingSink();
        var dispatcher = new OpenConnectReadyEdgeDispatcher();
        var ready = ReadyHeartbeat();

        dispatcher.Dispatch(ready, sink, CancellationToken.None, _ => { });
        dispatcher.Dispatch(ready, sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.ReadyCount);

        dispatcher.Dispatch(SampleShot(), sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.ShotCount);

        dispatcher.Dispatch(ready, sink, CancellationToken.None, _ => { });
        Assert.Equal(2, sink.ReadyCount);
    }

    [Fact]
    public void FirstNotReadyHeartbeat_FiresWaiting_ThenNotReadySupersedes()
    {
        var sink = new RecordingSink();
        var dispatcher = new OpenConnectReadyEdgeDispatcher();
        var notReady = NotReadyHeartbeat();

        // Code 201 is often absent — first connected+not-ready heartbeat → Waiting.
        dispatcher.Dispatch(notReady, sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.WaitingCount);
        Assert.Equal(0, sink.NotReadyCount);

        // Next not-ready must supersede Waiting (do not stay stuck on loading).
        dispatcher.Dispatch(notReady, sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.WaitingCount);
        Assert.Equal(1, sink.NotReadyCount);

        dispatcher.Dispatch(ReadyHeartbeat(), sink, CancellationToken.None, _ => { });
        dispatcher.Dispatch(notReady, sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.ReadyCount);
        Assert.Equal(2, sink.NotReadyCount);
        Assert.Equal(1, sink.WaitingCount);
    }

    [Fact]
    public void ShotWithoutContainsFlag_StillFiresWhenMetricsPresent()
    {
        var sink = new RecordingSink();
        var dispatcher = new OpenConnectReadyEdgeDispatcher();
        var shot = new ShotPayload
        {
            BallData = new BallData { Speed = 140, Hla = -2.5 },
            ShotDataOptions = new ShotDataOptions { ContainsBallData = false }
        };

        dispatcher.Dispatch(shot, sink, CancellationToken.None, _ => { });
        Assert.Equal(1, sink.ShotCount);
        Assert.Equal(0, sink.ReadyCount);
    }

    private static ShotPayload ReadyHeartbeat() =>
        new()
        {
            ShotDataOptions = new ShotDataOptions
            {
                IsHeartBeat = true,
                LaunchMonitorBallDetected = true,
                LaunchMonitorIsReady = true
            }
        };

    private static ShotPayload NotReadyHeartbeat() =>
        new()
        {
            ShotDataOptions = new ShotDataOptions
            {
                IsHeartBeat = true,
                LaunchMonitorBallDetected = false,
                LaunchMonitorIsReady = false
            }
        };

    private static ShotPayload SampleShot() =>
        new()
        {
            BallData = new BallData { Speed = 150, Hla = 2 },
            ShotDataOptions = new ShotDataOptions { ContainsBallData = true }
        };

    private sealed class RecordingSink : IShotEventSink
    {
        public int ShotCount { get; private set; }
        public int ReadyCount { get; private set; }
        public int NotReadyCount { get; private set; }
        public int WaitingCount { get; private set; }

        public Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
        {
            ShotCount++;
            return Task.CompletedTask;
        }

        public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
        {
            ReadyCount++;
            return Task.CompletedTask;
        }

        public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default)
        {
            NotReadyCount++;
            return Task.CompletedTask;
        }

        public Task OnWaitingAsync(CancellationToken cancellationToken = default)
        {
            WaitingCount++;
            return Task.CompletedTask;
        }
    }
}
