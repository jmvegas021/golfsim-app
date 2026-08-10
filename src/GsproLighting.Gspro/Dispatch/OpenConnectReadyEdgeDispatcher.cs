using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Dispatch;

/// <summary>
/// Edge-triggers Ready / Not Ready from Open Connect status (including heartbeats)
/// so repeated BallDetected=true heartbeats do not restart the Ready intro.
/// Shots always fire; after a shot the next Ready edge is allowed again.
/// </summary>
public sealed class OpenConnectReadyEdgeDispatcher
{
    private readonly object _gate = new();
    /// <summary>null = unknown, true = ready, false = not ready.</summary>
    private bool? _isReady;

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

        if (!TryEnterNotReadyEdge())
            return;
        SinkCallDispatcher.Fire(() => sink.OnBallNotReadyAsync(cancellationToken), onError);
    }

    private void ClearReadyState()
    {
        lock (_gate)
            _isReady = null;
    }

    private bool TryEnterReadyEdge()
    {
        lock (_gate)
        {
            if (_isReady == true)
                return false;
            _isReady = true;
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
            return true;
        }
    }
}
