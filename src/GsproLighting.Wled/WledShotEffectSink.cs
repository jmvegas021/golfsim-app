using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Drives curated WLED animations from live shot, ready, and player events.
/// Basic Idle/Waiting ambient uses WLED Ripple via HTTP; curated flashes return to that hold.
/// Solid curated holds use DRGB keepalive so WLED realtime timeout (~5s) cannot drop the bay.
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink
{
    private readonly IWledOutput _output;
    private readonly Func<EffectConfig> _effects;
    private readonly Func<WledConfig> _wledConfig;
    private readonly ShotEffectMapper _mapper = new();
    private readonly LedAnimationPlayer _animationPlayer;
    private readonly LiveShotAnimationRequestFactory _requestFactory = new();
    private readonly PreviewHoldKeepalive _keepalive;
    private readonly WledHttpClient _httpClient;
    private readonly Action<string>? _logFailure;
    private readonly Action? _onTakeover;
    private readonly object _gate = new();
    private CancellationTokenSource? _activeEffectCts;
    private bool _readyIdleActive;

    public WledShotEffectSink(
        IWledOutput output,
        Func<EffectConfig> effects,
        Func<WledConfig>? wledConfig = null,
        PreviewHoldKeepalive? keepalive = null,
        WledHttpClient? httpClient = null,
        Action<string>? logFailure = null,
        Action? onTakeover = null)
    {
        _output = output;
        _effects = effects;
        _wledConfig = wledConfig ?? (() => new WledConfig());
        _animationPlayer = new LedAnimationPlayer(output);
        _keepalive = keepalive ?? new PreviewHoldKeepalive();
        _httpClient = httpClient ?? new WledHttpClient();
        _logFailure = logFailure;
        _onTakeover = onTakeover;
    }

    public async Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        if (!shot.HasBallData &&
            shot.BallData?.Speed is null &&
            shot.BallData?.CarryDistance is null &&
            shot.BallData?.SideSpin is null)
            return;

        var effects = _effects();
        var plan = _mapper.MapPlan(shot, effects);
        var holdDuration = plan.IsPutt
            ? TimeSpan.FromMilliseconds(2200)
            : TimeSpan.FromMilliseconds(1600);
        await RunEffectAsync(
            isReady: false,
            debounceReady: false,
            async token =>
            {
                await PlayShotAsync(plan, token).ConfigureAwait(false);
                await HoldShotColorAsync(plan, holdDuration, token).ConfigureAwait(false);
                await HoldIdleAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Code != 201)
            return;

        await RunEffectAsync(
            isReady: null,
            debounceReady: false,
            async token =>
            {
                await PlayConfiguredSlotAsync(_effects().Player, token).ConfigureAwait(false);
                await Task.Delay(900, token).ConfigureAwait(false);
                await HoldIdleAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        await RunEffectAsync(
            isReady: true,
            debounceReady: true,
            async token =>
            {
                await PlayReadyIntroAsync(token).ConfigureAwait(false);
                await HoldIdleAsync(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Holds the Waiting slot — used before any Connect ready/not-ready signal has arrived
    /// (Connect state genuinely unknown), as distinct from the Idle hold used once ready.
    /// </summary>
    public async Task HoldWaitingAsync(CancellationToken cancellationToken = default)
    {
        await RunEffectAsync(
            isReady: null,
            debounceReady: false,
            token => HoldSlotAsync(_effects().Waiting, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task OnBallNotReadyAsync(CancellationToken cancellationToken = default)
    {
        await RunEffectAsync(
            isReady: false,
            debounceReady: false,
            async token =>
            {
                var slot = _effects().NotReady;
                await PlayConfiguredSlotAsync(slot, token).ConfigureAwait(false);
                if (slot.Mode == EffectMode.Curated)
                {
                    var dimBrightness = (byte)Math.Max(1, _wledConfig().Brightness / 3);
                    await _keepalive.HoldSolidAsync(
                            _output,
                            slot.Color,
                            dimBrightness,
                            duration: null,
                            token)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task PlayShotAsync(ShotLightPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Slot.Mode != EffectMode.Curated)
            return Task.CompletedTask;

        var request = _requestFactory.Create(plan, _wledConfig());
        return _animationPlayer.PlayAsync(request, cancellationToken);
    }

    private Task PlayReadyIntroAsync(CancellationToken cancellationToken)
    {
        // Ready transition stays a curated flash; Idle slot itself is Ripple ambient hold.
        var intro = EffectSlot.Curated(_effects().Idle.Color, EffectAnimations.CenterToOutside);
        return _animationPlayer.PlayAsync(intro, _wledConfig(), cancellationToken: cancellationToken);
    }

    private Task PlayConfiguredSlotAsync(EffectSlot slot, CancellationToken cancellationToken)
    {
        if (slot.Mode == EffectMode.Curated)
            return _animationPlayer.PlayAsync(slot, _wledConfig(), cancellationToken: cancellationToken);

        if (slot.Mode == EffectMode.WledPreset)
        {
            var request = WledPresetRequest.FromSlot(slot, _wledConfig().Brightness);
            return _httpClient.ApplyPresetAsync(_wledConfig().ControllerIp, request, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private Task HoldShotColorAsync(
        ShotLightPlan plan,
        TimeSpan holdDuration,
        CancellationToken cancellationToken)
    {
        if (plan.Slot.Mode != EffectMode.Curated)
            return Task.CompletedTask;

        return _keepalive.HoldSolidAsync(
            _output,
            plan.Slot.Color,
            _wledConfig().Brightness,
            holdDuration,
            cancellationToken);
    }

    private Task HoldIdleAsync(CancellationToken cancellationToken = default) =>
        HoldSlotAsync(_effects().Idle, cancellationToken);

    /// <summary>
    /// Re-holds Idle ambient after the live controller IP changes while Connect is already Ready.
    /// </summary>
    public Task HoldIdleForConnectionChangeAsync(CancellationToken cancellationToken = default) =>
        RunEffectAsync(
            isReady: true,
            debounceReady: false,
            HoldIdleAsync,
            cancellationToken);

    /// <summary>
    /// Stops Waiting/Idle/NotReady keepalive (and any in-flight shot) so manual Preview / Quick
    /// Control owns the strip without HTTP Ripple stomping DRGB frames.
    /// </summary>
    public void CancelActiveEffects()
    {
        lock (_gate)
        {
            _readyIdleActive = false;
            _activeEffectCts?.Cancel();
        }
    }

    private Task HoldSlotAsync(EffectSlot slot, CancellationToken cancellationToken)
    {
        if (slot.Mode == EffectMode.WledPreset)
            return HoldPresetAsync(slot, duration: null, cancellationToken);

        if (slot.Mode != EffectMode.Curated)
            return Task.CompletedTask;

        return _keepalive.HoldSolidAsync(
            _output,
            slot.Color,
            _wledConfig().Brightness,
            duration: null,
            cancellationToken);
    }

    private Task HoldPresetAsync(
        EffectSlot slot,
        TimeSpan? duration,
        CancellationToken cancellationToken) =>
        _keepalive.HoldWhileAsync(
            ct =>
            {
                // Resolve IP/brightness on every keepalive tick — not once at hold start.
                // App launch begins HoldWaiting against the placeholder IP; when Connection
                // later commits the real controller, an in-flight hold must follow the new
                // address instead of hammering 192.168.1.50 forever.
                var config = _wledConfig();
                var request = WledPresetRequest.FromSlot(slot, config.Brightness);
                return _httpClient.ApplyPresetAsync(config.ControllerIp, request, ct);
            },
            duration,
            cancellationToken);

    private async Task RunEffectAsync(
        bool? isReady,
        bool debounceReady,
        Func<CancellationToken, Task> playEffect,
        CancellationToken cancellationToken)
    {
        // Never open DRGB/HTTP against the shipped placeholder / blank IP.
        if (!_wledConfig().HasConfiguredController)
            return;

        var linked = BeginEffect(isReady, debounceReady, cancellationToken);
        if (linked is null)
            return;

        try
        {
            // Live GSPro ownership beats an open Quick Control / Preview hold.
            try
            {
                _onTakeover?.Invoke();
            }
            catch
            {
                // Takeover must never block the live shot path.
            }

            await playEffect(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && linked.IsCancellationRequested)
        {
            // Superseded by a newer live event.
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HTTP Idle/Ripple (or other apply) failures must not tear down GSPro TCP pipes.
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

    private CancellationTokenSource? BeginEffect(
        bool? isReady,
        bool debounceReady,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (debounceReady && _readyIdleActive)
                return null;

            if (isReady is bool ready)
                _readyIdleActive = ready;

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
