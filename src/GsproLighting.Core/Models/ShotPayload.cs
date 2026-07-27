namespace GsproLighting.Core.Models;

/// <summary>
/// Launch-monitor → GSPro Open Connect shot / status message.
/// </summary>
public sealed class ShotPayload
{
    public string? DeviceId { get; set; }
    public string? Units { get; set; }
    public int? ShotNumber { get; set; }
    public string? ApiVersion { get; set; }
    public BallData? BallData { get; set; }
    public ClubData? ClubData { get; set; }
    public ShotDataOptions? ShotDataOptions { get; set; }

    public bool IsHeartBeat => ShotDataOptions?.IsHeartBeat == true;

    public bool HasBallData =>
        ShotDataOptions?.ContainsBallData == true && BallData is not null;

    public bool IsBallDetected => ShotDataOptions?.LaunchMonitorBallDetected == true;

    public double? SmashFactor
    {
        get
        {
            var ballSpeed = BallData?.Speed;
            var clubSpeed = ClubData?.Speed ?? ClubData?.SpeedAtImpact;
            if (ballSpeed is null or <= 0 || clubSpeed is null or <= 0)
                return null;
            return ballSpeed / clubSpeed;
        }
    }
}
