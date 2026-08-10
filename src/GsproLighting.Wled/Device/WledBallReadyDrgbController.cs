using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Owns superseding Ready / Not Ready DRGB sessions (intro + keepalive hold).
/// Shared by live BallReady events and the Preview Ready / Not Ready buttons.
/// </summary>
public sealed class WledBallReadyDrgbController : IDisposable
{
    private readonly DrgbStripAnimationPlayer _player;
    private readonly object _sessionGate = new();
    private CancellationTokenSource? _activeSession;
    private HeldPose _pose = HeldPose.None;
    private bool _isDisposed;

    public WledBallReadyDrgbController(IWledOutput output) =>
        _player = new DrgbStripAnimationPlayer(output);

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
        RunSupersedingAsync(
            async token =>
            {
                var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
                await _player.PlayAsync(frames, brightness, token).ConfigureAwait(false);
                SetPose(HeldPose.ReadyCenterBand);
                onHoldStarted?.Invoke();
                var hold = DrgbReadyFrameFactory.CreateHoldPixels(ledCount);
                await _player.HoldPixelsAsync(
                        hold,
                        brightness,
                        duration: null,
                        token,
                        sendInitialFrame: false)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task RunNotReadyAsync(
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null)
    {
        // Capture before BeginSession clears pose when superseding the Ready hold.
        var fromReady = CurrentPose == HeldPose.ReadyCenterBand;
        return RunSupersedingAsync(
            async token =>
            {
                var frames = fromReady
                    ? DrgbNotReadyFrameFactory.CreateFromReadyCenterBand(ledCount)
                    : DrgbNotReadyFrameFactory.CreateExpandFromDark(ledCount);
                await _player.PlayAsync(frames, brightness, token).ConfigureAwait(false);
                SetPose(HeldPose.NotReadyFull);
                onHoldStarted?.Invoke();
                await _player.HoldBreathingSolidAsync(
                        DrgbNotReadyFrameFactory.NotReadyRed,
                        brightness,
                        DrgbNotReadyFrameFactory.BreathingLevels,
                        TimeSpan.FromMilliseconds(
                            DrgbNotReadyFrameFactory.BreathingCadenceMilliseconds),
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public void CancelActive()
    {
        lock (_sessionGate)
        {
            _activeSession?.Cancel();
            _pose = HeldPose.None;
        }
    }

    public void Dispose()
    {
        lock (_sessionGate)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _activeSession?.Cancel();
            _pose = HeldPose.None;
        }
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

    private void SetPose(HeldPose pose)
    {
        lock (_sessionGate)
            _pose = pose;
    }

    public enum HeldPose
    {
        None,
        ReadyCenterBand,
        NotReadyFull
    }
}
