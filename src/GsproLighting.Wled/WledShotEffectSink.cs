using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Wled.Contracts;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled;

/// <summary>
/// Maps GSPro events to WLED: Ready / Not Ready / hit directions use DDP streaming.
/// Loading / Waiting uses native WLED Ripple over HTTP (<c>live:false</c>).
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink, IDisposable
{
    /// <summary>Ball ready — full-intensity green (DDP Ready hold).</summary>
    public static readonly RgbColor ReadyColor = RgbColor.FromRgb(0, 255, 0);

    /// <summary>Ball not ready — solid red (DDP Not Ready hold).</summary>
    public static readonly RgbColor NotReadyColor = RgbColor.FromRgb(255, 0, 0);

    /// <summary>GSPro loading / start — aqua tint for native WLED Ripple (HTTP).</summary>
    public static readonly RgbColor WaitingColor = RgbColor.FromRgb(0, 200, 220);

    private readonly Func<WledConfig> _wledConfig;
    private readonly Func<EffectConfig> _effectConfig;
    private readonly ShotEffectMapper _shotMapper = new();
    private readonly IWledOutput _output;
    private readonly WledBallReadyDrgbController _readyDrgb;
    private readonly WledDirectionDrgbController _directionDrgb;
    private readonly WledHttpStateAnimationManager _animationManager;
    private readonly bool _ownsAnimationManager;
    private readonly bool _ownsReadyDrgb;
    private readonly Action<string>? _logFailure;
    private readonly Action? _onTakeover;

    public WledShotEffectSink(
        Func<WledConfig> wledConfig,
        IWledOutput output,
        WledHttpStateAnimationManager? animationManager = null,
        WledBallReadyDrgbController? readyDrgb = null,
        Action<string>? logFailure = null,
        Action? onTakeover = null,
        Func<EffectConfig>? effectConfig = null)
    {
        _wledConfig = wledConfig;
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _effectConfig = effectConfig ?? (() => new EffectConfig());
        _animationManager = animationManager ?? new WledHttpStateAnimationManager();
        _ownsAnimationManager = animationManager is null;
        _readyDrgb = readyDrgb ?? new WledBallReadyDrgbController(_output);
        _ownsReadyDrgb = readyDrgb is null;
        _directionDrgb = new WledDirectionDrgbController(_readyDrgb);
        _logFailure = logFailure;
        _onTakeover = onTakeover;
    }

    public Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        if (!shot.HasBallData && !shot.HasPlayableBallMetrics)
            return Task.CompletedTask;

        return RunDrgbEffectAsync(
            (config, token) =>
            {
                var plan = _shotMapper.MapPlan(shot, _effectConfig());
                var direction = ShotEffectMapper.ApplyInvertLeftRight(
                    plan.Direction,
                    config.InvertLeftRight);
                return _directionDrgb.RunDirectionAsync(
                    direction,
                    config.LedCount,
                    config.Brightness,
                    token,
                    tuning: _effectConfig().StatusTuning.Direction,
                    notReadyFallbackTuning: _effectConfig().StatusTuning.NotReady);
            },
            cancellationToken);
    }

    public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        // Code 201 = player / start-screen info from GSPro → native WLED Ripple (HTTP).
        // Also rare in R50 log-watch sessions — see OnWaitingAsync for Connect-loading edges.
        if (response.Code != 201)
            return Task.CompletedTask;

        return OnWaitingAsync(cancellationToken);
    }

    public Task OnWaitingAsync(CancellationToken cancellationToken = default) =>
        RunEffectAsync(
            async (config, token) =>
            {
                // Cancel DDP Ready/Not Ready/direction so HTTP Ripple can own the strip.
                _readyDrgb.CancelActive();
                await _animationManager.ApplyWaitingRippleAsync(
                        config.ControllerIp,
                        config.Brightness,
                        WaitingColor,
                        tuning: _effectConfig().StatusTuning.Waiting,
                        cancellationToken: token)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default) =>
        RunDrgbEffectAsync(
            (config, token) => _readyDrgb.RunReadyAsync(
                config.LedCount,
                config.Brightness,
                token,
                tuning: _effectConfig().StatusTuning.Ready),
            cancellationToken);

    public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default) =>
        RunDrgbEffectAsync(
            (config, token) => _readyDrgb.RunNotReadyAsync(
                config.LedCount,
                config.Brightness,
                token,
                tuning: _effectConfig().StatusTuning.NotReady),
            cancellationToken);

    /// <summary>
    /// Explicit waiting hold (preview / reconnect). Live triggers: Code 201 and
    /// Connect-loading edges (<see cref="OnWaitingAsync"/>).
    /// No-ops when WLED is not configured (avoids launch spam).
    /// </summary>
    public Task HoldWaitingAsync(CancellationToken cancellationToken = default) =>
        OnWaitingAsync(cancellationToken);

    /// <summary>
    /// Skeleton: ambient Idle restart removed after IP change.
    /// </summary>
    public Task HoldIdleForConnectionChangeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>Cancels DDP Ready/Not Ready/Waiting/direction holds and any in-flight HTTP animation.</summary>
    public void CancelActiveEffects()
    {
        _readyDrgb.CancelActive();
        _animationManager.CancelActive();
    }

    /// <summary>
    /// Cancels leftover HTTP animation, then runs a DDP status/direction effect.
    /// </summary>
    private Task RunDrgbEffectAsync(
        Func<WledConfig, CancellationToken, Task> action,
        CancellationToken cancellationToken) =>
        RunEffectAsync(
            (config, token) =>
            {
                _animationManager.CancelActive();
                return action(config, token);
            },
            cancellationToken);

    private async Task RunEffectAsync(
        Func<WledConfig, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var config = _wledConfig();
        if (!config.HasConfiguredController)
            return;

        try
        {
            InvokeTakeover();
            // Point shared DDP UDP at this snapshot before Ready/Not Ready / solids run —
            // Config.Wled can lag the Connection textbox until SyncWledConnectionLive runs.
            _output.Configure(config);
            await action(config, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer live event.
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogEffectFailure(ex);
        }
    }

    public void Dispose()
    {
        if (_ownsReadyDrgb)
            _readyDrgb.Dispose();
        if (_ownsAnimationManager)
            _animationManager.Dispose();
    }

    private void InvokeTakeover()
    {
        try
        {
            _onTakeover?.Invoke();
        }
        catch
        {
            // Takeover must never block the live shot path.
        }
    }

    private void LogEffectFailure(Exception ex)
    {
        var message = $"WLED effect failed: {ex.Message}";
        Console.WriteLine($"[wled] {message}");
        try
        {
            _logFailure?.Invoke(message);
        }
        catch
        {
            // Logging must never break the live effect path.
        }
    }
}
