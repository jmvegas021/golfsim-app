using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Serializes HTTP writers. Ready / Not Ready / hit-direction status cues are DDP-only
/// (<see cref="WledBallReadyDrgbController"/>). Loading / Waiting uses native WLED Ripple
/// over HTTP (<c>live:false</c>).
/// </summary>
public sealed class WledHttpStateAnimationManager : IDisposable
{
    internal const string StatusCueQuarantineMessage =
        "HTTP Ready / Not Ready / hit-direction cues are quarantined. " +
        "Use WledBallReadyDrgbController (DDP) for live and Preview status holds.";

    private readonly WledDeviceClient _client;
    private readonly WledSolidHttpApplier _solidApplier;
    private readonly bool _ownsClient;
    private readonly SemaphoreSlim _writerGate = new(1, 1);
    private readonly object _sessionGate = new();
    private CancellationTokenSource? _activeSession;
    private IReadOnlyList<WledNamedEntry>? _cachedEffects;
    private bool _isDisposed;

    public WledHttpStateAnimationManager(WledDeviceClient? client = null)
    {
        _client = client ?? new WledDeviceClient();
        _ownsClient = client is null;
        _solidApplier = new WledSolidHttpApplier(_client);
    }

    [Obsolete(StatusCueQuarantineMessage)]
    public Task RunNotReadyAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default)
    {
        _ = (controllerIp, ledCount, brightness, cancellationToken);
        return Task.FromException(new NotSupportedException(StatusCueQuarantineMessage));
    }

    [Obsolete(StatusCueQuarantineMessage)]
    public Task RunReadyAsync(
        string controllerIp,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default)
    {
        _ = (controllerIp, ledCount, brightness, cancellationToken);
        return Task.FromException(new NotSupportedException(StatusCueQuarantineMessage));
    }

    [Obsolete(StatusCueQuarantineMessage)]
    public Task RunHitDirectionAsync(
        string controllerIp,
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default)
    {
        _ = (controllerIp, direction, ledCount, brightness, cancellationToken);
        return Task.FromException(new NotSupportedException(StatusCueQuarantineMessage));
    }

    public Task ApplySolidAsync(
        string controllerIp,
        RgbColor color,
        byte brightness,
        int ledCount,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => _solidApplier.ApplySolidAsync(
                controllerIp,
                color,
                brightness,
                ledCount,
                token),
            cancellationToken);

    public Task ApplyOffAsync(
        string controllerIp,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => _solidApplier.ApplyOffAsync(controllerIp, token),
            cancellationToken);

    /// <summary>
    /// Loading / start: native WLED Ripple via HTTP with <c>live:false</c>.
    /// Resolves the Ripple FX id from the controller catalog when possible.
    /// Optional <paramref name="tuning"/> maps Speed/Intensity/Layers onto sx/ix/brightness.
    /// </summary>
    public Task ApplyWaitingRippleAsync(
        string controllerIp,
        byte brightness,
        RgbColor? color = null,
        StatusEffectStateTuning? tuning = null,
        CancellationToken cancellationToken = default) =>
        RunSupersedingAsync(
            token => ApplyWaitingRippleCoreAsync(
                controllerIp,
                brightness,
                color,
                tuning,
                token),
            cancellationToken);

    public void CancelActive()
    {
        lock (_sessionGate)
            _activeSession?.Cancel();
    }

    private async Task ApplyWaitingRippleCoreAsync(
        string controllerIp,
        byte brightness,
        RgbColor? color,
        StatusEffectStateTuning? tuning,
        CancellationToken cancellationToken)
    {
        var effects = _cachedEffects;
        if (effects is null)
        {
            try
            {
                effects = await _client.GetEffectsAsync(controllerIp, cancellationToken)
                    .ConfigureAwait(false);
                _cachedEffects = effects;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                effects = null;
            }
        }

        var fxId = WledEffectIdResolver.ResolveRippleFxId(effects);
        var tint = color ?? RgbColor.FromRgb(0, 200, 220);
        var (speed, intensity, scaledBrightness) =
            DrgbStatusEffectParams.ResolveWaitingRipple(tuning, brightness);
        var slot = EffectConfig.CreateRippleAmbient(tint);
        slot.WledFxId = fxId;
        slot.WledOptions ??= new WledPresetOptions();
        slot.WledOptions.Speed = speed;
        slot.WledOptions.Intensity = intensity;
        var request = WledPresetRequest.FromSlot(slot, scaledBrightness);
        await _client.ApplyPresetRequestAsync(controllerIp, request, cancellationToken)
            .ConfigureAwait(false);
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
