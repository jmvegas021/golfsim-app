namespace GsproLighting.Core.Models;

/// <summary>
/// Launch-monitor → GSPro Open Connect shot / status message.
/// Also used for Garmin Connect log ball-metrics mapped into the same shape.
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

    /// <summary>Garmin Connect spinType (e.g. putting / normal).</summary>
    public string? SpinType { get; set; }

    /// <summary>Set when log context or spinType indicates a putt.</summary>
    public bool? IsPutting { get; set; }

    /// <summary>Smash from Connect JSON when club/ball speeds are not both present.</summary>
    public double? MeasuredSmashFactor { get; set; }

    public bool IsHeartBeat => ShotDataOptions?.IsHeartBeat == true;

    public bool HasBallData =>
        ShotDataOptions?.ContainsBallData == true && BallData is not null;

    public bool IsBallDetected => ShotDataOptions?.LaunchMonitorBallDetected == true;

    public double? SmashFactor
    {
        get
        {
            if (MeasuredSmashFactor is double measured)
                return measured;

            var ballSpeed = BallData?.Speed;
            var clubSpeed = ClubData?.Speed ?? ClubData?.SpeedAtImpact;
            if (ballSpeed is null or <= 0 || clubSpeed is null or <= 0)
                return null;
            return ballSpeed / clubSpeed;
        }
    }
}
