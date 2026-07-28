using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Core.Services;

public sealed class CompositeShotEventSink : IShotEventSink
{
    private readonly IReadOnlyList<IShotEventSink> _sinks;

    public CompositeShotEventSink(params IShotEventSink[] sinks)
    {
        _sinks = sinks;
    }

    public async Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.OnShotAsync(shot, cancellationToken);
    }

    public async Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.OnPlayerInfoAsync(response, cancellationToken);
    }

    public async Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.OnBallReadyAsync(payload, cancellationToken);
    }

    public async Task OnBallNotReadyAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
            await sink.OnBallNotReadyAsync(cancellationToken);
    }
}
