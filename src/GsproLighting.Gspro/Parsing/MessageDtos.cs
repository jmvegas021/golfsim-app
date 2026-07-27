using System.Text.Json.Serialization;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Parsing;

internal sealed class ShotPayloadDto
{
    [JsonPropertyName("DeviceID")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("Units")]
    public string? Units { get; set; }

    [JsonPropertyName("ShotNumber")]
    public int? ShotNumber { get; set; }

    [JsonPropertyName("APIversion")]
    public string? ApiVersion { get; set; }

    [JsonPropertyName("BallData")]
    public BallDataDto? BallData { get; set; }

    [JsonPropertyName("ClubData")]
    public ClubDataDto? ClubData { get; set; }

    [JsonPropertyName("ShotDataOptions")]
    public ShotDataOptionsDto? ShotDataOptions { get; set; }

    public ShotPayload ToModel() => new()
    {
        DeviceId = DeviceId,
        Units = Units,
        ShotNumber = ShotNumber,
        ApiVersion = ApiVersion,
        BallData = BallData?.ToModel(),
        ClubData = ClubData?.ToModel(),
        ShotDataOptions = ShotDataOptions?.ToModel()
    };
}

internal sealed class BallDataDto
{
    [JsonPropertyName("Speed")]
    public double? Speed { get; set; }

    [JsonPropertyName("SpinAxis")]
    public double? SpinAxis { get; set; }

    [JsonPropertyName("TotalSpin")]
    public double? TotalSpin { get; set; }

    [JsonPropertyName("BackSpin")]
    public double? BackSpin { get; set; }

    [JsonPropertyName("SideSpin")]
    public double? SideSpin { get; set; }

    [JsonPropertyName("HLA")]
    public double? Hla { get; set; }

    [JsonPropertyName("VLA")]
    public double? Vla { get; set; }

    [JsonPropertyName("CarryDistance")]
    public double? CarryDistance { get; set; }

    public BallData ToModel() => new()
    {
        Speed = Speed,
        SpinAxis = SpinAxis,
        TotalSpin = TotalSpin,
        BackSpin = BackSpin,
        SideSpin = SideSpin,
        Hla = Hla,
        Vla = Vla,
        CarryDistance = CarryDistance
    };
}

internal sealed class ClubDataDto
{
    [JsonPropertyName("Speed")]
    public double? Speed { get; set; }

    [JsonPropertyName("AngleOfAttack")]
    public double? AngleOfAttack { get; set; }

    [JsonPropertyName("FaceToTarget")]
    public double? FaceToTarget { get; set; }

    [JsonPropertyName("Lie")]
    public double? Lie { get; set; }

    [JsonPropertyName("Loft")]
    public double? Loft { get; set; }

    [JsonPropertyName("Path")]
    public double? Path { get; set; }

    [JsonPropertyName("SpeedAtImpact")]
    public double? SpeedAtImpact { get; set; }

    [JsonPropertyName("VerticalFaceImpact")]
    public double? VerticalFaceImpact { get; set; }

    [JsonPropertyName("HorizontalFaceImpact")]
    public double? HorizontalFaceImpact { get; set; }

    [JsonPropertyName("ClosureRate")]
    public double? ClosureRate { get; set; }

    public ClubData ToModel() => new()
    {
        Speed = Speed,
        AngleOfAttack = AngleOfAttack,
        FaceToTarget = FaceToTarget,
        Lie = Lie,
        Loft = Loft,
        Path = Path,
        SpeedAtImpact = SpeedAtImpact,
        VerticalFaceImpact = VerticalFaceImpact,
        HorizontalFaceImpact = HorizontalFaceImpact,
        ClosureRate = ClosureRate
    };
}

internal sealed class ShotDataOptionsDto
{
    [JsonPropertyName("ContainsBallData")]
    public bool ContainsBallData { get; set; }

    [JsonPropertyName("ContainsClubData")]
    public bool ContainsClubData { get; set; }

    [JsonPropertyName("LaunchMonitorIsReady")]
    public bool? LaunchMonitorIsReady { get; set; }

    [JsonPropertyName("LaunchMonitorBallDetected")]
    public bool? LaunchMonitorBallDetected { get; set; }

    [JsonPropertyName("IsHeartBeat")]
    public bool? IsHeartBeat { get; set; }

    public ShotDataOptions ToModel() => new()
    {
        ContainsBallData = ContainsBallData,
        ContainsClubData = ContainsClubData,
        LaunchMonitorIsReady = LaunchMonitorIsReady,
        LaunchMonitorBallDetected = LaunchMonitorBallDetected,
        IsHeartBeat = IsHeartBeat
    };
}

internal sealed class GsproResponseDto
{
    [JsonPropertyName("Code")]
    public int? Code { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("Player")]
    public PlayerInfoDto? Player { get; set; }

    public GsproResponse ToModel(Dictionary<string, object?> extensions) => new()
    {
        Code = Code,
        Message = Message,
        Player = Player?.ToModel(),
        Extensions = extensions
    };
}

internal sealed class PlayerInfoDto
{
    [JsonPropertyName("Handed")]
    public string? Handed { get; set; }

    [JsonPropertyName("Club")]
    public string? Club { get; set; }

    public PlayerInfo ToModel() => new()
    {
        Handed = Handed,
        Club = Club
    };
}
