using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled;

namespace GsproLighting.Ui.Forms;

public sealed partial class PreviewTabPanel
{
    private async Task ApplyWaitingAnimationAsync()
    {
        var wled = _resolveWled();
        if (!wled.HasConfiguredController)
        {
            SetStatus("Set a real controller IP on Connection first.", isError: true);
            RefreshIpLabel();
            return;
        }

        var ip = wled.ControllerIp.Trim();
        var generation = ++_statusGeneration;
        // Match live OnWaitingAsync: cancel DDP so HTTP Ripple owns the strip.
        CancelLiveEffects();
        _stateManager.CancelActive();
        var brightness = wled.Brightness == 0 ? (byte)1 : wled.Brightness;
        SetStatus($"Running Waiting · HTTP Ripple → {ip}…");
        try
        {
            await _stateManager.ApplyWaitingRippleAsync(
                    ip,
                    brightness,
                    WledShotEffectSink.WaitingColor,
                    tuning: _resolveStatusTuning().Waiting)
                .ConfigureAwait(true);
            if (generation == _statusGeneration)
                SetStatus($"Waiting · HTTP Ripple OK · {ip} (StatusTuning sx/ix/layers)");
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer Preview action or CancelLiveEffects.
        }
        catch (Exception ex)
        {
            if (generation != _statusGeneration)
                return;
            SetStatus(ex.Message, isError: true);
            _logWledFailure?.Invoke("preview-waiting", ex.Message);
        }
    }

    private Task ApplyNotReadyAnimationAsync() =>
        ApplyDdpHoldAsync(
            "Not Ready · DDP",
            (wled, tuning, token, onHold) => _readyDrgb.RunNotReadyAsync(
                Math.Max(1, wled.LedCount),
                wled.Brightness == 0 ? (byte)1 : wled.Brightness,
                token,
                onHold,
                tuning.NotReady),
            holdStatus: "Not Ready · DDP band shimmer");

    private Task ApplyReadyAnimationAsync() =>
        ApplyDdpHoldAsync(
            "Ready · DDP",
            (wled, tuning, token, onHold) => _readyDrgb.RunReadyAsync(
                Math.Max(1, wled.LedCount),
                wled.Brightness == 0 ? (byte)1 : wled.Brightness,
                token,
                onHold,
                tuning.Ready),
            holdStatus: "Ready · DDP band shimmer");

    private Task ApplyHitDirectionAsync(ShotDirection direction, string label) =>
        ApplyDdpHoldAsync(
            label,
            (wled, tuning, token, onHold) => _directionDrgb.RunDirectionAsync(
                direction,
                Math.Max(1, wled.LedCount),
                wled.Brightness == 0 ? (byte)1 : wled.Brightness,
                token,
                onHold,
                tuning.Direction),
            holdStatus: $"{label} · DDP band shimmer");

    private async Task ApplyDdpHoldAsync(
        string label,
        Func<WledConfig, StatusEffectTuning, CancellationToken, Action, Task> play,
        string holdStatus)
    {
        // Sync Connection → shared DdpWledOutput before UDP (same path Quick Control uses).
        var wled = _resolveWled();
        if (!wled.HasConfiguredController)
        {
            SetStatus("Set a real controller IP on Connection first.", isError: true);
            RefreshIpLabel();
            return;
        }

        var tuning = _resolveStatusTuning();
        var ip = wled.ControllerIp.Trim();
        var target = $"{ip}:{wled.UdpPort} · {Math.Max(1, wled.LedCount)} LEDs";
        var generation = ++_statusGeneration;
        // Cancel HTTP only — do not CancelActive on DDP so Ready→Not Ready can morph.
        _stateManager.CancelActive();
        _configureOutput?.Invoke(wled);
        SetStatus($"Running {label} → {target}…");
        try
        {
            await play(
                    wled,
                    tuning,
                    CancellationToken.None,
                    () =>
                    {
                        if (generation == _statusGeneration)
                            SetStatus($"{holdStatus} · {target}");
                    })
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer Ready/Not Ready or CancelLiveEffects.
        }
        catch (Exception ex)
        {
            if (generation != _statusGeneration)
                return;
            SetStatus(ex.Message, isError: true);
            _logWledFailure?.Invoke("preview-animation", ex.Message);
        }
    }

    private void CancelLiveEffects()
    {
        try
        {
            _cancelLiveEffects?.Invoke();
        }
        catch
        {
            // Manual Preview must still work if cancellation reporting fails.
        }
    }
}
