using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Edge-triggers Waiting / Ready / Not Ready for a single Connect log stream.
/// Ready and Not Ready always supersede Waiting; Waiting never clobbers known status.
/// </summary>
public sealed class ConnectReadyWaitingEdgeState
{
    /// <summary>null = unknown, true = ready, false = not-ready.</summary>
    private bool? _isReady;
    private bool _waitingShown;
    private bool _lookingForGarminWaitingArmed;

    public ConnectParseResult? TryEmitWaiting(string trimmed)
    {
        var connectedToLm = trimmed.Contains("Connected to LM", StringComparison.OrdinalIgnoreCase);
        var looking = trimmed.Contains("Looking for Garmin", StringComparison.OrdinalIgnoreCase);
        var deviceClosed = trimmed.Contains("Device close connection", StringComparison.OrdinalIgnoreCase);

        if (deviceClosed)
        {
            ResetUnknown();
            return null;
        }

        if (!connectedToLm && !looking)
            return null;

        if (looking && !connectedToLm)
        {
            if (_isReady is not null || _waitingShown || _lookingForGarminWaitingArmed)
                return ConnectParseResult.Ignore;
            _lookingForGarminWaitingArmed = true;
            _waitingShown = true;
            return ConnectParseResult.ForWaiting(trimmed);
        }

        if (_isReady is not null || _waitingShown)
            return ConnectParseResult.Ignore;

        _waitingShown = true;
        _lookingForGarminWaitingArmed = false;
        return ConnectParseResult.ForWaiting(trimmed);
    }

    public ConnectParseResult EmitReady(string trimmed)
    {
        ClearWaitingFlags();
        if (_isReady == true)
            return ConnectParseResult.Ignore;

        _isReady = true;
        return ConnectParseResult.ForReady(CreateReadyPayload(), trimmed);
    }

    public ConnectParseResult EmitNotReady(string trimmed)
    {
        ClearWaitingFlags();
        if (_isReady == false)
            return ConnectParseResult.Ignore;

        _isReady = false;
        return ConnectParseResult.ForNotReady(trimmed);
    }

    public void ClearAfterShot()
    {
        _isReady = null;
        ClearWaitingFlags();
    }

    private void ResetUnknown()
    {
        _isReady = null;
        ClearWaitingFlags();
    }

    private void ClearWaitingFlags()
    {
        _waitingShown = false;
        _lookingForGarminWaitingArmed = false;
    }

    private static ShotPayload CreateReadyPayload() => new()
    {
        DeviceId = "GarminR50",
        ShotDataOptions = new ShotDataOptions
        {
            LaunchMonitorBallDetected = true,
            LaunchMonitorIsReady = true,
            IsHeartBeat = false
        }
    };
}
