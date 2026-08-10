using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled;

/// <summary>
/// Maps GSPro events to the focused HTTP state manager. No DRGB, ambient, or idle restart.
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink, IDisposable
{
    /// <summary>Ball ready — green.</summary>
    public static readonly RgbColor ReadyColor = RgbColor.FromRgb(0, 220, 0);

    /// <summary>Ball not ready — dim red (also sent at reduced brightness).</summary>
    public static readonly RgbColor NotReadyColor = RgbColor.FromRgb(180, 30, 30);

    /// <summary>Player info — blue.</summary>
    public static readonly RgbColor PlayerColor = RgbColor.FromRgb(40, 120, 255);

    private readonly Func<WledConfig> _wledConfig;
    private readonly Func<EffectConfig> _effectConfig;
    private readonly ShotEffectMapper _shotMapper = new();
    private readonly WledHttpStateAnimationManager _animationManager;
    private readonly bool _ownsAnimationManager;
    private readonly Action<string>? _logFailure;
    private readonly Action? _onTakeover;

    public WledShotEffectSink(
        Func<WledConfig> wledConfig,
        WledHttpStateAnimationManager? animationManager = null,
        Action<string>? logFailure = null,
        Action? onTakeover = null,
        Func<EffectConfig>? effectConfig = null)
    {
        _wledConfig = wledConfig;
        _effectConfig = effectConfig ?? (() => new EffectConfig());
        _animationManager = animationManager ?? new WledHttpStateAnimationManager();
        _ownsAnimationManager = animationManager is null;
        _logFailure = logFailure;
        _onTakeover = onTakeover;
    }

    public Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        if (!shot.HasBallData &&
            shot.BallData?.Speed is null &&
            shot.BallData?.CarryDistance is null &&
            shot.BallData?.SideSpin is null)
            return Task.CompletedTask;

        return RunEffectAsync(
            (config, token) =>
            {
                var plan = _shotMapper.MapPlan(shot, _effectConfig());
                var direction = ShotEffectMapper.ApplyInvertLeftRight(
                    plan.Direction,
                    config.InvertLeftRight);
                return _animationManager.RunHitDirectionAsync(
                    config.ControllerIp,
                    direction,
                    config.LedCount,
                    config.Brightness,
                    token);
            },
            cancellationToken);
    }

    public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Code != 201)
            return Task.CompletedTask;

        return ApplySolidOnceAsync(PlayerColor, cancellationToken);
    }

    public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default) =>
        RunEffectAsync(
            (config, token) => _animationManager.RunReadyAsync(
                config.ControllerIp,
                config.LedCount,
                config.Brightness,
                token),
            cancellationToken);

    public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default) =>
        RunEffectAsync(
            (config, token) => _animationManager.RunNotReadyAsync(
                config.ControllerIp,
                config.LedCount,
                config.Brightness,
                token),
            cancellationToken);

    /// <summary>
    /// Skeleton: ambient Waiting removed — no HTTP on unknown Connect state.
    /// </summary>
    public Task HoldWaitingAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Skeleton: ambient Idle restart removed after IP change.
    /// </summary>
    public Task HoldIdleForConnectionChangeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>Cancels the current loop or in-flight frame before manual control takes over.</summary>
    public void CancelActiveEffects() => _animationManager.CancelActive();

    private async Task ApplySolidOnceAsync(
        RgbColor color,
        CancellationToken cancellationToken) =>
        await RunEffectAsync(
            (config, token) => _animationManager.ApplySolidAsync(
                config.ControllerIp,
                color,
                config.Brightness,
                token),
            cancellationToken).ConfigureAwait(false);

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
