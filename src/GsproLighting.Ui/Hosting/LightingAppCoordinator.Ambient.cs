using GsproLighting.Core.Models;

namespace GsproLighting.Ui.Hosting;

public sealed partial class LightingAppCoordinator
{
    /// <summary>
    /// Pushes the Connection tab's current (possibly unsaved) field values into the live WLED
    /// config and re-points the shared DRGB/UDP output at them — without writing to disk. The
    /// DRGB output caches its target IP/port from whatever WledConfig was last passed to
    /// Configure(), which previously only happened on Save/Reload/construction; every live
    /// action (Test lights, Idle glow, Preview, Quick Control, and actual GSPro shot reactions)
    /// shares that same cached target, so editing the IP without hitting Save silently left them
    /// pointed at the old value even though the WLED tab (which reads the field directly) worked.
    /// Settings also auto-persists when the Connection IP is committed (field leave / scan pick)
    /// so the effect sink and next launch keep the new address.
    /// </summary>
    public void SyncWledConnectionLive(string controllerIp, int udpPort, int ledCount, byte brightness, bool invertLeftRight)
    {
        if (string.IsNullOrWhiteSpace(controllerIp))
            return;

        var previousIp = Config.Wled.ControllerIp;
        Config.Wled.ControllerIp = controllerIp;
        Config.Wled.UdpPort = udpPort;
        Config.Wled.LedCount = ledCount;
        Config.Wled.Brightness = brightness;
        Config.Wled.InvertLeftRight = invertLeftRight;
        _wled.Configure(Config.Wled);

        // Keepalive holds capture their work each tick, but also restart ambient so the first
        // post after an IP change doesn't wait for the next 2.5s keepalive interval.
        if (!string.Equals(previousIp, controllerIp, StringComparison.OrdinalIgnoreCase))
            RestartAmbientAfterConnectionChange();
    }

    /// <summary>
    /// Re-applies the ambient hold appropriate for current Connect readiness against the new
    /// controller IP (Waiting / Idle / NotReady). Cancels any in-flight hold via the sink gate.
    /// </summary>
    private void RestartAmbientAfterConnectionChange()
    {
        if (!Config.Wled.HasConfiguredController)
            return;

        try
        {
            _ = BallReadyState switch
            {
                BallReadyState.Ready => _effectSink.HoldIdleForConnectionChangeAsync(),
                BallReadyState.NotReady => _effectSink.OnBallNotReadyAsync(),
                _ => _effectSink.HoldWaitingAsync()
            };
        }
        catch (Exception ex)
        {
            CrashLog.Write("RestartAmbientAfterConnectionChange", ex);
        }
    }

}
