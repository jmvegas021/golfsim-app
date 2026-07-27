using GsproLighting.Core.Models;

namespace GsproLighting.Core.Contracts;

public interface IShotEventSink
{
    Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default);
    Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default);
    Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default);
}
