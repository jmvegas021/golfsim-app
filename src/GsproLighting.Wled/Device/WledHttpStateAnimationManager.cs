using GsproLighting.Core.Config;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Serializes all HTTP state writers and gives each new action ownership by canceling the
/// previous action before it can send another frame.
/// </summary>
public sealed class WledHttpStateAnimationManager : IDisposable
{
    private readonly WledDeviceClient _client;
    private readonly WledSolidHttpApplier _solidApplier;
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
            token => RunFramesAsync(
                controllerIp,
                WledHttpAnimationFrameFactory.CreateReadySequence(ledCount, brightness),
                token),
            cancellationToken);

    public Task ApplySolidAsync(
        string controllerIp,
        RgbColor color,
        byte brightness,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => _solidApplier.ApplySolidAsync(controllerIp, color, brightness, token),
            cancellationToken);

    public Task ApplyOffAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => _solidApplier.ApplyOffAsync(controllerIp, token),
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

    private async Task RunNotReadyFramesAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken)
    {
        var expand = WledHttpAnimationFrameFactory.CreateNotReadyExpandSequence(ledCount, brightness);
        await RunFramesAsync(controllerIp, expand, cancellationToken).ConfigureAwait(false);
        var cycle = WledHttpAnimationFrameFactory.CreateRedBreathingCycle(brightness);
        while (true)
            await RunFramesAsync(controllerIp, cycle, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunFramesAsync(
        string controllerIp,
        IReadOnlyList<WledHttpAnimationFrame> frames,
        CancellationToken cancellationToken)
    {
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _client.ApplyStateBodyAsync(controllerIp, frame.Body, cancellationToken)
                .ConfigureAwait(false);
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
}
