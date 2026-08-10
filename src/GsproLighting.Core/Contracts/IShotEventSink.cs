using GsproLighting.Core.Models;

namespace GsproLighting.Core.Contracts;

public interface IShotEventSink
{
    Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default);
    Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default);
    Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default);
    Task OnBallNotReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GSPro / Connect loading before Ready (aqua Waiting hold). Optional for sinks
    /// that only care about shots.
    /// </summary>
    Task OnWaitingAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
