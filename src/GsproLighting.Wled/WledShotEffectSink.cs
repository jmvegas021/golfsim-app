using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Device;

namespace GsproLighting.Wled;

/// <summary>
/// Skeleton live sink: one HTTP solid POST per GSPro event. No DRGB animations,
/// no Ripple ambient, no keepalive, no return-to-idle.
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink
{
    /// <summary>Ball ready — green.</summary>
    public static readonly RgbColor ReadyColor = RgbColor.FromRgb(0, 220, 0);

    /// <summary>Ball not ready — dim red (also sent at reduced brightness).</summary>
    public static readonly RgbColor NotReadyColor = RgbColor.FromRgb(180, 30, 30);

    /// <summary>Any shot — bright green.</summary>
    public static readonly RgbColor ShotColor = RgbColor.FromRgb(0, 255, 80);

    /// <summary>Player info — blue.</summary>
    public static readonly RgbColor PlayerColor = RgbColor.FromRgb(40, 120, 255);

    private readonly Func<WledConfig> _wledConfig;
    private readonly WledSolidHttpApplier _applier;
    private readonly Action<string>? _logFailure;
    private readonly Action? _onTakeover;
    private readonly object _gate = new();
    private CancellationTokenSource? _activeEffectCts;

    public WledShotEffectSink(
        Func<WledConfig> wledConfig,
        WledSolidHttpApplier? applier = null,
        Action<string>? logFailure = null,
        Action? onTakeover = null)
    {
        _wledConfig = wledConfig;
        _applier = applier ?? new WledSolidHttpApplier();
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

        return ApplySolidOnceAsync(ShotColor, useDimBrightness: false, cancellationToken);
    }

    public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Code != 201)
            return Task.CompletedTask;

        return ApplySolidOnceAsync(PlayerColor, useDimBrightness: false, cancellationToken);
    }

    public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default) =>
        ApplySolidOnceAsync(ReadyColor, useDimBrightness: false, cancellationToken);

    public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default) =>
        ApplySolidOnceAsync(NotReadyColor, useDimBrightness: true, cancellationToken);

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

    /// <summary>Cancels any in-flight solid POST so a newer event can own the gate.</summary>
    public void CancelActiveEffects()
    {
        lock (_gate)
            _activeEffectCts?.Cancel();
    }

    private async Task ApplySolidOnceAsync(
        RgbColor color,
        bool useDimBrightness,
        CancellationToken cancellationToken)
    {
        var config = _wledConfig();
        if (!config.HasConfiguredController)
            return;

        var linked = BeginEffect(cancellationToken);
        if (linked is null)
            return;

        try
        {
            try
            {
                _onTakeover?.Invoke();
            }
            catch
            {
                // Takeover must never block the live shot path.
            }

            var brightness = useDimBrightness
                ? (byte)Math.Max(1, config.Brightness / 3)
                : config.Brightness;

            await _applier
                .ApplySolidAsync(config.ControllerIp, color, brightness, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && linked.IsCancellationRequested)
        {
            // Superseded by a newer live event.
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogEffectFailure(ex);
        }
        finally
        {
            CompleteEffect(linked);
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

    private CancellationTokenSource? BeginEffect(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _activeEffectCts?.Cancel();
            _activeEffectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return _activeEffectCts;
        }
    }

    private void CompleteEffect(CancellationTokenSource completed)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_activeEffectCts, completed))
                _activeEffectCts = null;
        }

        completed.Dispose();
    }
}
