using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Drives curated WLED animations from live shot, ready, and player events.
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink
{
    private readonly IWledOutput _output;
    private readonly Func<EffectConfig> _effects;
    private readonly Func<WledConfig> _wledConfig;
    private readonly ShotEffectMapper _mapper = new();
    private readonly LedAnimationPlayer _animationPlayer;
    private readonly LiveShotAnimationRequestFactory _requestFactory = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _activeEffectCts;
    private bool _readyIdleActive;

    public WledShotEffectSink(
        IWledOutput output,
        Func<EffectConfig> effects,
        Func<WledConfig>? wledConfig = null)
    {
        _output = output;
        _effects = effects;
        _wledConfig = wledConfig ?? (() => new WledConfig());
        _animationPlayer = new LedAnimationPlayer(output);
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
                await Task.Delay(holdDuration, token).ConfigureAwait(false);
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
                await PlayConfiguredSlotAsync(_effects().Idle, token).ConfigureAwait(false);
                await HoldIdleAsync(token).ConfigureAwait(false);
            },
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
                    await _output.SendSolidAsync(slot.Color, dimBrightness, token).ConfigureAwait(false);
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

    private Task PlayConfiguredSlotAsync(EffectSlot slot, CancellationToken cancellationToken)
    {
        if (slot.Mode == EffectMode.Curated)
            return _animationPlayer.PlayAsync(slot, _wledConfig(), cancellationToken: cancellationToken);

        // Presets are preview-only; the UI prevents selecting them for live slots.
        return Task.CompletedTask;
    }

    private Task HoldIdleAsync(CancellationToken cancellationToken)
    {
        var idle = _effects().Idle;
        return idle.Mode == EffectMode.Curated
            ? _output.SendSolidAsync(idle.Color, _wledConfig().Brightness, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task RunEffectAsync(
        bool? isReady,
        bool debounceReady,
        Func<CancellationToken, Task> playEffect,
        CancellationToken cancellationToken)
    {
        var linked = BeginEffect(isReady, debounceReady, cancellationToken);
        if (linked is null)
            return;

        try
        {
            await playEffect(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer live event.
        }
        finally
        {
            CompleteEffect(linked);
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
