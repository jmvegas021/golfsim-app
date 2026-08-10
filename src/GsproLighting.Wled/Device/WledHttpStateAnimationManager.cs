using GsproLighting.Core.Config;
using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Serializes all HTTP state writers and gives each new action ownership by canceling the
/// previous action before it can send another frame.
/// </summary>
public sealed class WledHttpStateAnimationManager : IDisposable
{
    private readonly WledDeviceClient _client;
    private readonly WledSolidHttpApplier _solidApplier;
    private readonly WledHttpVisualStateTracker _visualTracker;
    private readonly bool _ownsClient;
    private readonly SemaphoreSlim _writerGate = new(1, 1);
    private readonly object _sessionGate = new();
    private CancellationTokenSource? _activeSession;
    private bool _isDisposed;

    public WledHttpStateAnimationManager(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient();
        _ownsClient = client is null;
        _solidApplier = new WledSolidHttpApplier(_client);
        _visualTracker = new WledHttpVisualStateTracker();
    }

    public Task RunNotReadyAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => RunNotReadyFramesAsync(controllerIp, ledCount, brightness, token),
            cancellationToken);

    public Task RunReadyAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => RunReadyFramesAsync(controllerIp, ledCount, brightness, token),
            cancellationToken);

    public Task RunHitDirectionAsync(
        string controllerIp,
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => RunFramesAsync(
                controllerIp,
                WledHttpAnimationFrameFactory.CreateHitDirectionSequence(
                    direction,
                    ledCount,
                    brightness),
                token,
                WledHttpAnimationFrameFactory.ResolveHitColor(direction)),
            cancellationToken);

    public Task ApplySolidAsync(
        string controllerIp,
        RgbColor color,
        byte brightness,
        int ledCount,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            async token =>
            {
                await _solidApplier.ApplySolidAsync(
                        controllerIp,
                        color,
                        brightness,
                        ledCount,
                        token)
                    .ConfigureAwait(false);
                _visualTracker.RememberSolid(color, brightness);
            },
            cancellationToken);

    public Task ApplyOffAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            async token =>
            {
                await _solidApplier.ApplyOffAsync(controllerIp, token).ConfigureAwait(false);
                _visualTracker.Clear();
            },
            cancellationToken);

    public void CancelActive()
    {
        lock (_sessionGate)
            _activeSession?.Cancel();
    }

    public void Dispose()
    {
        lock (_sessionGate)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _activeSession?.Cancel();
        }

        var writerStopped = _writerGate.Wait(TimeSpan.FromSeconds(5));
        _solidApplier.Dispose();
        if (_ownsClient && writerStopped)
            _client.Dispose();
        if (writerStopped)
            _writerGate.Release();
    }

    private async Task RunReadyFramesAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken)
    {
        var target = WledHttpAnimationFrameFactory.ReadyGreen;
        if (_visualTracker.TryGetSolid(out var fromColor, out var fromBrightness))
        {
            // Morph on a full-strip solid, then concentrate → full solid green.
            var morph = WledHttpAnimationFrameFactory.CreateColorTransitionTracked(
                fromColor,
                fromBrightness,
                target,
                brightness,
                ledCount);
            await RunTrackedFramesAsync(controllerIp, morph, cancellationToken)
                .ConfigureAwait(false);
            var chase = WledHttpReadyAnimationBuilder.CreateReadyChaseFromFullSequence(
                ledCount,
                brightness);
            await RunFramesAsync(controllerIp, chase, cancellationToken, target)
                .ConfigureAwait(false);
            return;
        }

        var ready = WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, brightness);
        await RunFramesAsync(controllerIp, ready, cancellationToken, target)
            .ConfigureAwait(false);
    }

    private async Task RunNotReadyFramesAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken)
    {
        var target = WledHttpAnimationFrameFactory.NotReadyRed;
        if (_visualTracker.TryGetSolid(out var fromColor, out var fromBrightness))
        {
            // Full-strip morph clears any Ready geometry, then center-half → edges expand.
            var morph = WledHttpAnimationFrameFactory.CreateColorTransitionTracked(
                fromColor,
                fromBrightness,
                target,
                brightness,
                ledCount);
            await RunTrackedFramesAsync(controllerIp, morph, cancellationToken)
                .ConfigureAwait(false);
            var fromHalf = WledHttpAnimationFrameFactory.CreateNotReadyExpandFromHalfSequence(
                ledCount,
                brightness);
            await RunFramesAsync(controllerIp, fromHalf, cancellationToken, target)
                .ConfigureAwait(false);
        }
        else
        {
            var expand = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(
                ledCount,
                brightness);
            await RunFramesAsync(controllerIp, expand, cancellationToken, target)
                .ConfigureAwait(false);
        }

        // Breathe bri only on full-strip solid red (fx 0) — never partial center bands.
        var cycle = WledHttpAnimationFrameFactory.CreateRedBreathingTracked(brightness, ledCount);
        while (true)
            await RunTrackedFramesAsync(controllerIp, cycle, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task RunTrackedFramesAsync(
        string controllerIp,
        IReadOnlyList<WledHttpTrackedFrame> frames,
        CancellationToken cancellationToken)
    {
        foreach (var tracked in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _client.ApplyStateBodyAsync(controllerIp, tracked.Frame.Body, cancellationToken)
                .ConfigureAwait(false);
            _visualTracker.RememberSolid(tracked.Color, tracked.Brightness);
            if (tracked.Frame.Duration > TimeSpan.Zero)
                await Task.Delay(tracked.Frame.Duration, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunFramesAsync(
        string controllerIp,
        IReadOnlyList<WledHttpAnimationFrame> frames,
        CancellationToken cancellationToken,
        RgbColor? trackColor = null)
    {
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _client.ApplyStateBodyAsync(controllerIp, frame.Body, cancellationToken)
                .ConfigureAwait(false);
            if (trackColor is not null)
                _visualTracker.RememberSolid(trackColor, ReadFrameBrightness(frame.Body));
            if (frame.Duration > TimeSpan.Zero)
                await Task.Delay(frame.Duration, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunSupersedingAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var session = BeginSession(cancellationToken);
        var hasWriterGate = false;
        try
        {
            await _writerGate.WaitAsync(session.Token).ConfigureAwait(false);
            hasWriterGate = true;
            await action(session.Token).ConfigureAwait(false);
        }
        finally
        {
            if (hasWriterGate)
                _writerGate.Release();
            CompleteSession(session);
        }
    }

    private CancellationTokenSource BeginSession(CancellationToken cancellationToken)
    {
        lock (_sessionGate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _activeSession?.Cancel();
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

    private static byte ReadFrameBrightness(object body)
    {
        if (body is Dictionary<string, object?> dictionary &&
            dictionary.TryGetValue("bri", out var value) &&
            value is not null)
            return Convert.ToByte(value);

        return 0;
    }
}
