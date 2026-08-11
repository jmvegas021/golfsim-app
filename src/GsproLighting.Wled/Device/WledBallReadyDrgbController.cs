using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Owns superseding Ready / Not Ready / Waiting / hit-direction DDP sessions
/// (intro + band shimmer hold). Shared by live events and Preview buttons so
/// supersede never fights across HTTP vs DDP.
/// </summary>
public sealed class WledBallReadyDrgbController : IDisposable
{
    private readonly DrgbStripAnimationPlayer _player;
    private readonly DrgbDirectionMinHoldGate _directionHold;
    private readonly object _sessionGate = new();
    private CancellationTokenSource? _activeSession;
    private HeldPose _pose = HeldPose.None;
    private bool _isDisposed;

    public WledBallReadyDrgbController(
        IWledOutput output,
        DrgbDirectionMinHoldGate? directionHold = null)
    {
        _player = new DrgbStripAnimationPlayer(output);
        _directionHold = directionHold ?? new DrgbDirectionMinHoldGate();
    }

    public DrgbDirectionMinHoldGate DirectionHold => _directionHold;

    public HeldPose CurrentPose
    {
        get
        {
            lock (_sessionGate)
                return _pose;
        }
    }

    public Task RunReadyAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null,
        StatusEffectStateTuning? tuning = null)
    {
        var parameters = DrgbStatusEffectParams.FromTuning(tuning);
        return RunStatusAsync(
            token => PlayIntroThenHoldAsync(
                DrgbReadyFrameFactory.CreateReadySequence(ledCount, parameters),
                DrgbBandShimmerEffect.ForReady(ledCount, parameters),
                ledCount,
                brightness,
                HeldPose.ReadyCenterBand,
                onHoldStarted,
                token),
            cancellationToken);
    }

    public Task RunNotReadyAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null,
        StatusEffectStateTuning? tuning = null)
    {
        // Capture before BeginSession clears pose when superseding the Ready hold.
        var fromReady = CurrentPose == HeldPose.ReadyCenterBand;
        return RunStatusAsync(
            token => PlayNotReadyHoldAsync(
                ledCount,
                brightness,
                tuning,
                onHoldStarted,
                token,
                fromReady),
            cancellationToken);
    }

    /// <summary>
    /// Legacy DDP aqua Waiting hold for tests / explicit DDP exercises.
    /// Live and Preview Waiting use HTTP Ripple via
    /// <see cref="WledHttpStateAnimationManager.ApplyWaitingRippleAsync"/>.
    /// </summary>
    public Task RunWaitingAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null,
        StatusEffectStateTuning? tuning = null)
    {
        var parameters = DrgbStatusEffectParams.FromTuning(tuning);
        return RunStatusAsync(
            token => PlayIntroThenHoldAsync(
                DrgbWaitingFrameFactory.CreateWaitingSequence(ledCount, parameters),
                DrgbBandShimmerEffect.ForWaiting(ledCount, parameters),
                ledCount,
                brightness,
                HeldPose.WaitingAqua,
                onHoldStarted,
                token),
            cancellationToken);
    }

    public Task RunDirectionAsync(
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null,
        StatusEffectStateTuning? tuning = null,
        StatusEffectStateTuning? notReadyFallbackTuning = null)
    {
        var parameters = DrgbStatusEffectParams.FromTuning(tuning);
        return RunSupersedingAsync(
            token =>
            {
                // Arm before intro so Ready/Not Ready arriving mid-slide cannot cancel
                // the hit cue (R50 logs Not Ready ~2–3s after the Force line).
                _directionHold.Arm();
                // Live OnShotAsync passes StatusTuning.NotReady so that when min-hold
                // elapses with nothing queued, we synthesize Not Ready (R50/GSPro often
                // never send it after a Force shot). Real Ready / Not Ready via TryDefer
                // replace this fallback (latest wins). Waiting / CancelActive clear it.
                // Preview omits notReadyFallbackTuning so direction can be held manually.
                // Use CancellationToken.None so the waiter outlives this direction session
                // when BeginSession cancels it to start the deferred status.
                if (notReadyFallbackTuning is not null)
                {
                    var fallbackTuning = notReadyFallbackTuning;
                    _ = _directionHold.TryDefer(
                        deferToken => RunSupersedingAsync(
                            holdToken => PlayNotReadyHoldAsync(
                                ledCount,
                                brightness,
                                fallbackTuning,
                                onHoldStarted: null,
                                holdToken,
                                fromReady: false),
                            deferToken),
                        CancellationToken.None);
                }

                return PlayIntroThenHoldAsync(
                    DrgbDirectionFrameFactory.CreateDirectionSequence(
                        direction,
                        ledCount,
                        parameters),
                    DrgbBandShimmerEffect.ForDirection(direction, ledCount, parameters),
                    ledCount,
                    brightness,
                    ToDirectionPose(direction),
                    onHoldStarted,
                    token);
            },
            cancellationToken);
    }

    private Task PlayNotReadyHoldAsync(
        int ledCount,
        byte brightness,
        StatusEffectStateTuning? tuning,
        Action? onHoldStarted,
        CancellationToken token,
        bool fromReady)
    {
        var parameters = DrgbStatusEffectParams.FromTuning(tuning);
        var intro = fromReady
            ? DrgbNotReadyFrameFactory.CreateFromReadyCenterBand(ledCount, parameters)
            : DrgbNotReadyFrameFactory.CreateExpandFromDark(ledCount, parameters);
        return PlayIntroThenHoldAsync(
            intro,
            DrgbBandShimmerEffect.ForNotReady(ledCount, parameters),
            ledCount,
            brightness,
            HeldPose.NotReadyFull,
            onHoldStarted,
            token);
    }

    public void CancelActive()
    {
        lock (_sessionGate)
            StopSessionUnlocked();
    }

    public void Dispose()
    {
        lock (_sessionGate)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            StopSessionUnlocked();
        }
    }

    private async Task PlayIntroThenHoldAsync(
        IReadOnlyList<LedAnimationFrame> introFrames,
        IDrgbHoldEffect holdEffect,
        int ledCount,
        byte brightness,
        HeldPose pose,
        Action? onHoldStarted,
        CancellationToken token,
        Action? onIntroComplete = null)
    {
        _ = brightness;
        var intensity = DrgbReadyFrameFactory.MaxIntensityBrightness;
        await _player.PlayAsync(introFrames, intensity, token).ConfigureAwait(false);
        SetPose(pose);
        onIntroComplete?.Invoke();
        onHoldStarted?.Invoke();
        await _player.HoldEffectAsync(
                holdEffect,
                ledCount,
                intensity,
                duration: null,
                token)
            .ConfigureAwait(false);
    }

    private Task RunStatusAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var deferred = _directionHold.TryDefer(
            token => RunSupersedingAsync(action, token),
            cancellationToken);
        return deferred ?? RunSupersedingAsync(action, cancellationToken);
    }

    private async Task RunSupersedingAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var session = BeginSession(cancellationToken);
        try
        {
            await action(session.Token).ConfigureAwait(false);
        }
        finally
        {
            CompleteSession(session);
        }
    }

    private CancellationTokenSource BeginSession(CancellationToken cancellationToken)
    {
        lock (_sessionGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _activeSession?.Cancel();
            _pose = HeldPose.None;
            _activeSession = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return _activeSession;
        }
    }

    private void CompleteSession(CancellationTokenSource session)
    {
        lock (_sessionGate)
        {
            if (ReferenceEquals(_activeSession, session))
                _activeSession = null;
        }

        session.Dispose();
    }

    private void StopSessionUnlocked()
    {
        _directionHold.Clear();
        _activeSession?.Cancel();
        _pose = HeldPose.None;
    }

    private void SetPose(HeldPose pose)
    {
        lock (_sessionGate)
            _pose = pose;
    }

    private static HeldPose ToDirectionPose(ShotDirection direction) =>
        direction switch
        {
            ShotDirection.Left => HeldPose.DirectionLeft,
            ShotDirection.Right => HeldPose.DirectionRight,
            _ => HeldPose.DirectionCenter
        };

    public enum HeldPose
    {
        None,
        ReadyCenterBand,
        NotReadyFull,
        WaitingAqua,
        DirectionLeft,
        DirectionCenter,
        DirectionRight
    }
}
