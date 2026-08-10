using GsproLighting.Core.Models;

namespace GsproLighting.Wled.Device;

/// <summary>
/// Hit-direction DDP entry point. Delegates to the shared Ready/Not Ready/Direction
/// session owner so superseding stays on one DDP shimmer-hold path.
/// </summary>
public sealed class WledDirectionDrgbController
{
    private readonly WledBallReadyDrgbController _ddpSessions;

    public WledDirectionDrgbController(WledBallReadyDrgbController ddpSessions) =>
        _ddpSessions = ddpSessions ?? throw new ArgumentNullException(nameof(ddpSessions));

    public WledBallReadyDrgbController.HeldPose CurrentPose => _ddpSessions.CurrentPose;

    public Task RunDirectionAsync(
        ShotDirection direction,
        int ledCount,
        byte brightness,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null) =>
        _ddpSessions.RunDirectionAsync(
            direction,
            ledCount,
            brightness,
            cancellationToken,
            onHoldStarted);

    public void CancelActive() => _ddpSessions.CancelActive();
}
