using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Owns superseding Ready / Not Ready / hit-direction DDP sessions (intro + band shimmer hold).
/// Shared by live events and Preview buttons so supersede never fights across HTTP vs DDP.
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
                _ = brightness;
                var intensity = DrgbReadyFrameFactory.MaxIntensityBrightness;
                var frames = DrgbReadyFrameFactory.CreateReadySequence(ledCount);
                await _player.PlayAsync(frames, intensity, token).ConfigureAwait(false);
                SetPose(HeldPose.ReadyCenterBand);
                onHoldStarted?.Invoke();
                await _player.HoldEffectAsync(
                        DrgbBandShimmerEffect.ForReady(ledCount),
                        ledCount,
                        intensity,
                        duration: null,
                        token)
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
                _ = brightness;
                var intensity = DrgbReadyFrameFactory.MaxIntensityBrightness;
                var frames = fromReady
                    ? DrgbNotReadyFrameFactory.CreateFromReadyCenterBand(ledCount)
                    : DrgbNotReadyFrameFactory.CreateExpandFromDark(ledCount);
                await _player.PlayAsync(frames, intensity, token).ConfigureAwait(false);
                SetPose(HeldPose.NotReadyFull);
                onHoldStarted?.Invoke();
                await _player.HoldEffectAsync(
                        DrgbBandShimmerEffect.ForNotReady(ledCount),
                        ledCount,
                        intensity,
                        duration: null,
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task RunDirectionAsync(
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null) =>
        RunSupersedingAsync(
            async token =>
            {
                _ = brightness;
                var intensity = DrgbReadyFrameFactory.MaxIntensityBrightness;
                var frames = DrgbDirectionFrameFactory.CreateDirectionSequence(direction, ledCount);
                await _player.PlayAsync(frames, intensity, token).ConfigureAwait(false);
                SetPose(ToDirectionPose(direction));
                onHoldStarted?.Invoke();
                await _player.HoldEffectAsync(
                        DrgbBandShimmerEffect.ForDirection(direction, ledCount),
                        ledCount,
                        intensity,
                        duration: null,
                        token)
                    .ConfigureAwait(false);
            },
            cancellationToken);

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
        DirectionLeft,
        DirectionCenter,
        DirectionRight
    }
}
