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
        Action? onHoldStarted = null) =>
        RunStatusAsync(
            token => PlayIntroThenHoldAsync(
                DrgbReadyFrameFactory.CreateReadySequence(ledCount),
                DrgbBandShimmerEffect.ForReady(ledCount),
                ledCount,
                brightness,
                HeldPose.ReadyCenterBand,
                onHoldStarted,
                token),
            cancellationToken);

    public Task RunNotReadyAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null)
    {
        // Capture before BeginSession clears pose when superseding the Ready hold.
        var fromReady = CurrentPose == HeldPose.ReadyCenterBand;
        var intro = fromReady
            ? DrgbNotReadyFrameFactory.CreateFromReadyCenterBand(ledCount)
            : DrgbNotReadyFrameFactory.CreateExpandFromDark(ledCount);
        return RunStatusAsync(
            token => PlayIntroThenHoldAsync(
                intro,
                DrgbBandShimmerEffect.ForNotReady(ledCount),
                ledCount,
                brightness,
                HeldPose.NotReadyFull,
                onHoldStarted,
                token),
            cancellationToken);
    }

    /// <summary>
    /// GSPro loading / start-screen aqua ripple (full-strip center→out shimmer).
    /// Superseded by Ready / Not Ready / direction. Respects direction min-hold.
    /// </summary>
    public Task RunWaitingAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null) =>
        RunStatusAsync(
            token => PlayIntroThenHoldAsync(
                DrgbWaitingFrameFactory.CreateWaitingSequence(ledCount),
                DrgbBandShimmerEffect.ForWaiting(ledCount),
                ledCount,
                brightness,
                HeldPose.WaitingAqua,
                onHoldStarted,
                token),
            cancellationToken);

    public Task RunDirectionAsync(
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null) =>
        RunSupersedingAsync(
            token =>
            {
                // Arm before intro so Ready/Not Ready arriving mid-slide cannot cancel
                // the hit cue (R50 logs Not Ready ~2–3s after the Force line).
                _directionHold.Arm();
                return PlayIntroThenHoldAsync(
                    DrgbDirectionFrameFactory.CreateDirectionSequence(direction, ledCount),
                    DrgbBandShimmerEffect.ForDirection(direction, ledCount),
                    ledCount,
                    brightness,
                    ToDirectionPose(direction),
                    onHoldStarted,
                    token);
            },
            cancellationToken);

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
