using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Drives WLED solid colors from live shot / ready / player events.
/// </summary>
public sealed class WledShotEffectSink : IShotEventSink
{
    private readonly IWledOutput _output;
    private readonly Func<EffectConfig> _effects;
    private readonly ShotEffectMapper _mapper = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _flashCts;
    private bool _readyIdleActive;

    public WledShotEffectSink(IWledOutput output, Func<EffectConfig> effects)
    {
        _output = output;
        _effects = effects;
    }

    public async Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        if (!shot.HasBallData &&
            shot.BallData?.Speed is null &&
            shot.BallData?.CarryDistance is null &&
            shot.BallData?.SideSpin is null)
            return;

        lock (_gate)
            _readyIdleActive = false;

        var effects = _effects();
        var color = _mapper.Map(shot, effects);
        var isPutt = ShotEffectMapper.IsPutt(shot, effects);
        var holdMs = isPutt ? 2200 : 1600;
        await FlashAsync(color, holdMs, cancellationToken).ConfigureAwait(false);
    }

    public async Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Code != 201)
            return;
        await FlashAsync(_effects().Player, holdMs: 900, cancellationToken).ConfigureAwait(false);
    }

    public async Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        // Skip repeated ready keepalives — only pulse idle when entering ready.
        CancellationTokenSource linked;
        lock (_gate)
        {
            if (_readyIdleActive)
                return;

            _readyIdleActive = true;
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked = _flashCts;
        }

        var token = linked.Token;
        var effects = _effects();
        try
        {
            await _output.SendSolidAsync(effects.Player, cancellationToken: token).ConfigureAwait(false);
            await Task.Delay(500, token).ConfigureAwait(false);
            await _output.SendSolidAsync(effects.Idle, cancellationToken: token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer flash.
        }
    }

    public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default)
    {
        // Red light — clear ready gate so the next green pulse can fire; no ready glow.
        lock (_gate)
            _readyIdleActive = false;
        return Task.CompletedTask;
    }

    private async Task FlashAsync(
        RgbColor color,
        int holdMs,
        CancellationToken cancellationToken = default,
        bool returnToIdle = true)
    {
        CancellationTokenSource linked;
        lock (_gate)
        {
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked = _flashCts;
        }

        var token = linked.Token;
        try
        {
            await _output.SendSolidAsync(color, cancellationToken: token).ConfigureAwait(false);
            await Task.Delay(holdMs, token).ConfigureAwait(false);
            if (returnToIdle)
                await _output.SendSolidAsync(_effects().Idle, cancellationToken: token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer flash.
        }
    }
}
