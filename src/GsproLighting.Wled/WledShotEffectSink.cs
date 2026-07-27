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

    public WledShotEffectSink(IWledOutput output, Func<EffectConfig> effects)
    {
        _output = output;
        _effects = effects;
    }

    public async Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        if (!shot.HasBallData && shot.BallData?.Speed is null)
            return;

        var color = _mapper.Map(shot, _effects());
        await FlashAsync(color, holdMs: 1600, cancellationToken).ConfigureAwait(false);
    }

    public async Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        if (response.Code != 201)
            return;
        await FlashAsync(_effects().Player, holdMs: 900, cancellationToken).ConfigureAwait(false);
    }

    public async Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        await FlashAsync(_effects().Idle, holdMs: 700, cancellationToken: cancellationToken, returnToIdle: false)
            .ConfigureAwait(false);
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
