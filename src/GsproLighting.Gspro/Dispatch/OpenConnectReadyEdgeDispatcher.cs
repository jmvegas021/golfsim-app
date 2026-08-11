using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Dispatch;

/// <summary>
/// Edge-triggers Ready / Not Ready from Open Connect status (including heartbeats)
/// so repeated BallDetected=true heartbeats do not restart the Ready intro.
/// Shots always fire; after a shot the next Ready edge is allowed again.
/// Waiting fires once while connected+heartbeat before the first Ready edge
/// (Code 201 is rare / absent on many GSPro builds).
/// </summary>
public sealed class OpenConnectReadyEdgeDispatcher
{
    private readonly object _gate = new();
    /// <summary>null = unknown, true = ready, false = not ready.</summary>
    private bool? _isReady;
    private bool _waitingShown;

    public void Dispatch(
        ShotPayload shot,
        IShotEventSink sink,
        CancellationToken cancellationToken,
        Action<string> onError)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(onError);

        if (shot.HasBallData || shot.HasPlayableBallMetrics)
        {
            ClearReadyState();
            SinkCallDispatcher.Fire(() => sink.OnShotAsync(shot, cancellationToken), onError);
            return;
        }

        if (shot.IsBallDetected)
        {
            if (!TryEnterReadyEdge())
                return;
            SinkCallDispatcher.Fire(() => sink.OnBallReadyAsync(shot, cancellationToken), onError);
            return;
        }

        if (!shot.IndicatesNotReady)
            return;

        // Connected + not-ready heartbeat before any Ready → loading once.
        // Do not occupy the Not Ready edge — Ready/Not Ready must always supersede Waiting.
        if (TryEnterWaitingEdge())
        {
            SinkCallDispatcher.Fire(() => sink.OnWaitingAsync(cancellationToken), onError);
            return;
        }

        if (!TryEnterNotReadyEdge())
            return;
        SinkCallDispatcher.Fire(() => sink.OnBallNotReadyAsync(cancellationToken), onError);
    }

    private void ClearReadyState()
    {
        lock (_gate)
        {
            _isReady = null;
            _waitingShown = false;
        }
    }

    private bool TryEnterReadyEdge()
    {
        lock (_gate)
        {
            if (_isReady == true)
                return false;
            _isReady = true;
            _waitingShown = false;
            return true;
        }
    }

    private bool TryEnterNotReadyEdge()
    {
        lock (_gate)
        {
            if (_isReady == false)
                return false;
            _isReady = false;
            _waitingShown = false;
            return true;
        }
    }

    private bool TryEnterWaitingEdge()
    {
        lock (_gate)
        {
            // Only while readiness is still unknown and Waiting has not been shown.
            if (_isReady is not null || _waitingShown)
                return false;
            _waitingShown = true;
            // Leave _isReady null so the next Not Ready heartbeat can supersede Waiting.
            return true;
        }
    }
}
