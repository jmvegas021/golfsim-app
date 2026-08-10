using GsproLighting.Ui;

namespace GsproLighting.Ui.Hosting;

public sealed partial class LightingAppCoordinator
{
    /// <summary>
    /// Pushes the Connection tab's current (possibly unsaved) field values into the live WLED
    /// config and re-points the shared DDP/UDP output at them — without writing to disk.
    /// Skeleton: does not restart ambient after IP change (no Waiting/Idle posts).
    /// </summary>
    public void SyncWledConnectionLive(string controllerIp, int udpPort, int ledCount, byte brightness, bool invertLeftRight)
    {
        if (string.IsNullOrWhiteSpace(controllerIp))
            return;

        Config.Wled.ControllerIp = controllerIp;
        Config.Wled.UdpPort = udpPort;
        Config.Wled.LedCount = ledCount;
        Config.Wled.Brightness = brightness;
        Config.Wled.InvertLeftRight = invertLeftRight;
        _wled.Configure(Config.Wled);
    }

    /// <summary>
    /// Cancels any in-flight solid POST so Preview / tray can drive WLED immediately.
    /// </summary>
    public void SuspendLiveEffectsForManualControl()
    {
        try
        {
            _effectSink.CancelActiveEffects();
        }
        catch (Exception ex)
        {
            CrashLog.Write("SuspendLiveEffectsForManualControl", ex);
        }
    }

    /// <summary>
    /// Skeleton: ambient resume is cancel-only — no Idle/Waiting/NotReady restart.
    /// Animation libraries remain in tree but are not re-entered from here.
    /// </summary>
    public void ResumeAmbientLighting()
    {
        try
        {
            Preview.CancelActivePreview();
            _effectSink.CancelActiveEffects();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ResumeAmbientLighting", ex);
        }
    }
}
