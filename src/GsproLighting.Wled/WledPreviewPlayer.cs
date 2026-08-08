using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Reusable preview facade for curated animations and WLED effects.
/// Supports superseding previews, fade handoff, and DRGB / HTTP hold keepalive.
/// </summary>
public sealed class WledPreviewPlayer : IDisposable
{
    private readonly IWledOutput _output;
    private readonly LedAnimationPlayer _animationPlayer;
    private readonly WledHttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly PreviewTransitionFader _fader = new();
    private readonly PreviewHoldKeepalive _keepalive = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _activePreviewCts;
    private Task? _sessionTask;
    private RgbColor? _heldColor;
    private byte _heldBrightness;

    public WledPreviewPlayer(IWledOutput output, WledHttpClient? httpClient = null)
    {
        _output = output;
        _animationPlayer = new LedAnimationPlayer(output);
        _httpClient = httpClient ?? new WledHttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public async Task PlayEffectAsync(
        EffectSlot slot,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(config);

        if (slot.Mode == EffectMode.WledPreset)
        {
            var request = WledPresetRequest.FromSlot(slot, config.Brightness);
            await _httpClient.ApplyPresetAsync(config.ControllerIp, request, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _animationPlayer.PlayAsync(slot, config, direction, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Plays the effect, briefly holds, then clears (Effects-tab / tray test behavior).
    /// </summary>
    public async Task PreviewEffectAsync(
        EffectSlot slot,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        var linked = BeginPreview(cancellationToken);
        var session = RunSessionAsync(linked, async token =>
        {
            await PlayEffectAsync(slot, config, direction, token).ConfigureAwait(false);
            await Task.Delay(holdDuration ?? TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
            await _output.ClearAsync(token).ConfigureAwait(false);
            ClearHeldState();
        });
        await session.ConfigureAwait(false);
    }

    /// <summary>
    /// Plays the effect then holds until cancelled or <paramref name="holdDuration"/> elapses.
    /// Curated holds use DRGB keepalive; WLED presets re-apply over HTTP.
    /// </summary>
    public async Task PreviewAndHoldAsync(
        PreviewHoldPlan plan,
        WledConfig config,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);

        var linked = BeginPreview(cancellationToken);
        var holdReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = RunSessionAsync(linked, async token =>
        {
            await FadeFromHeldAsync(token).ConfigureAwait(false);
            await PlayEffectAsync(plan.Slot, config, plan.Direction, token).ConfigureAwait(false);

            if (plan.Slot.Mode == EffectMode.WledPreset)
            {
                RememberHold(plan.Slot.Color, plan.HoldBrightness);
                onHoldStarted?.Invoke();
                holdReady.TrySetResult();
                await HoldPresetAsync(
                        plan.Slot,
                        config,
                        plan.HoldBrightness,
                        holdDuration,
                        token,
                        sendInitialFrame: false)
                    .ConfigureAwait(false);
                return;
            }

            await PreviewHoldKeepalive.SendHoldFrameAsync(_output, plan, config, token)
                .ConfigureAwait(false);
            RememberHold(plan.Slot.Color, plan.HoldBrightness);
            onHoldStarted?.Invoke();
            holdReady.TrySetResult();

            await _keepalive.HoldAsync(
                    _output,
                    plan,
                    config,
                    holdDuration,
                    token,
                    sendInitialFrame: false)
                .ConfigureAwait(false);
        });

        await WaitForHoldOrSessionAsync(holdReady, session).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (holdDuration is not null)
        {
            await session.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Cancels any active preview and holds ready/idle ambient until superseded.
    /// </summary>
    public async Task StopAndHoldIdleAsync(
        EffectSlot idleSlot,
        WledConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idleSlot);
        ArgumentNullException.ThrowIfNull(config);

        var linked = BeginPreview(cancellationToken);
        var holdReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = RunSessionAsync(linked, async token =>
        {
            await FadeFromHeldAsync(token).ConfigureAwait(false);

            if (idleSlot.Mode == EffectMode.WledPreset)
            {
                RememberHold(idleSlot.Color, config.Brightness);
                holdReady.TrySetResult();
                await HoldPresetAsync(idleSlot, config, config.Brightness, duration: null, token)
                    .ConfigureAwait(false);
                return;
            }

            await _output.SendSolidAsync(idleSlot.Color, config.Brightness, token).ConfigureAwait(false);
            RememberHold(idleSlot.Color, config.Brightness);
            holdReady.TrySetResult();
            await _keepalive.HoldSolidAsync(
                    _output,
                    idleSlot.Color,
                    config.Brightness,
                    duration: null,
                    token,
                    sendInitialFrame: false)
                .ConfigureAwait(false);
        });

        await WaitForHoldOrSessionAsync(holdReady, session).ConfigureAwait(false);
    }

    public async Task PlaySweepAsync(
        RgbColor color,
        int ledCount,
        CancellationToken cancellationToken = default)
    {
        var linked = BeginPreview(cancellationToken);
        await RunSessionAsync(linked, async token =>
        {
            await _animationPlayer.PlayAsync(new LedAnimationRequest
            {
                Animation = EffectAnimations.Sweep,
                Color = color,
                LedCount = ledCount
            }, token).ConfigureAwait(false);
            await Task.Delay(250, token).ConfigureAwait(false);
            await _output.ClearAsync(token).ConfigureAwait(false);
            ClearHeldState();
        }).ConfigureAwait(false);
    }

    public async Task PlayIdleGlowAsync(RgbColor color, CancellationToken cancellationToken = default)
    {
        var linked = BeginPreview(cancellationToken);
        await RunSessionAsync(linked, async token =>
        {
            await _output.SendSolidAsync(color, cancellationToken: token).ConfigureAwait(false);
            await Task.Delay(800, token).ConfigureAwait(false);
            await _output.ClearAsync(token).ConfigureAwait(false);
            ClearHeldState();
        }).ConfigureAwait(false);
    }

    public void CancelActivePreview()
    {
        lock (_gate)
        {
            _activePreviewCts?.Cancel();
        }
    }

    public void Dispose()
    {
        CancelActivePreview();
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private Task HoldPresetAsync(
        EffectSlot slot,
        WledConfig config,
        byte brightness,
        TimeSpan? duration,
        CancellationToken cancellationToken,
        bool sendInitialFrame = true)
    {
        var request = WledPresetRequest.FromSlot(slot, brightness);
        return _keepalive.HoldWhileAsync(
            ct => _httpClient.ApplyPresetAsync(config.ControllerIp, request, ct),
            duration,
            cancellationToken,
            sendInitialFrame);
    }

    private async Task FadeFromHeldAsync(CancellationToken cancellationToken)
    {
        RgbColor? color;
        byte brightness;
        lock (_gate)
        {
            color = _heldColor;
            brightness = _heldBrightness;
        }

        if (color is null || brightness == 0)
            return;

        try
        {
            await _fader.FadeOutAsync(_output, color, brightness, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private void RememberHold(RgbColor color, byte brightness)
    {
        lock (_gate)
        {
            _heldColor = color;
            _heldBrightness = brightness;
        }
    }

    private void ClearHeldState()
    {
        lock (_gate)
        {
            _heldColor = null;
            _heldBrightness = 0;
        }
    }

    private CancellationTokenSource BeginPreview(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _activePreviewCts?.Cancel();
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activePreviewCts = linked;
            return linked;
        }
    }

    private async Task RunSessionAsync(CancellationTokenSource linked, Func<CancellationToken, Task> body)
    {
        var task = ExecuteSessionAsync(linked, body);
        lock (_gate)
            _sessionTask = task;
        await task.ConfigureAwait(false);
    }

    private async Task ExecuteSessionAsync(CancellationTokenSource linked, Func<CancellationToken, Task> body)
    {
        try
        {
            await body(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!linked.IsCancellationRequested)
        {
            // Outer token cancelled through linked — propagate.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer preview / Stop.
        }
        finally
        {
            CompletePreview(linked);
        }
    }

    private void CompletePreview(CancellationTokenSource completed)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_activePreviewCts, completed))
                _activePreviewCts = null;
        }

        completed.Dispose();
    }

    private static async Task WaitForHoldOrSessionAsync(TaskCompletionSource holdReady, Task session)
    {
        var finished = await Task.WhenAny(holdReady.Task, session).ConfigureAwait(false);
        if (ReferenceEquals(finished, session))
            await session.ConfigureAwait(false);
    }
}
