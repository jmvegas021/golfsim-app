using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Parsing;
using Xunit;

namespace GsproLighting.Tests;

/// <summary>
/// Realistic Open Connect JSON fixtures → HLA direction buckets for live lighting.
/// </summary>
public sealed class OpenConnectShotFixtureTests
{
    private readonly GsproMessageParser _parser = new();

    [Theory]
    [InlineData("01-drive-fade.json", ShotDirection.Right)]
    [InlineData("02-drive-draw.json", ShotDirection.Left)]
    public void FixtureShot_MapsHlaDirection(string fileName, ShotDirection expected)
    {
        var json = File.ReadAllText(FixturePath("shots", fileName));
        var shot = _parser.Parse("LM→GSPro", json).Shot;
        Assert.NotNull(shot);
        Assert.True(shot!.HasBallData);
        Assert.True(shot.HasPlayableBallMetrics);

        var direction = ShotEffectMapper.ClassifyDirection(
            shot.BallData!.Hla,
            centerHlaAbsDegrees: 1.5);

        Assert.Equal(expected, direction);
    }

    [Fact]
    public void BallDataWithoutContainsFlag_StillPlayableAfterParse()
    {
        const string json = """
            {
              "DeviceID": "Bridge",
              "ShotNumber": 3,
              "BallData": { "Speed": 150.0, "HLA": -3.2, "CarryDistance": 240.0 }
            }
            """;

        var shot = _parser.Parse("LM→GSPro", json).Shot;
        Assert.NotNull(shot);
        Assert.True(shot!.HasBallData);
        Assert.True(shot.HasPlayableBallMetrics);
        Assert.Equal(ShotDirection.Left, ShotEffectMapper.ClassifyDirection(shot.BallData!.Hla, 1.5));
    }

    [Fact]
    public void HeartbeatNotReadyFlags_IndicateNotReady()
    {
        const string json = """
            {
              "DeviceID": "LM",
              "ShotDataOptions": {
                "ContainsBallData": false,
                "LaunchMonitorIsReady": false,
                "LaunchMonitorBallDetected": false,
                "IsHeartBeat": true
              }
            }
            """;

        var shot = _parser.Parse("LM→GSPro", json).Shot;
        Assert.NotNull(shot);
        Assert.True(shot!.IsHeartBeat);
        Assert.True(shot.IndicatesNotReady);
        Assert.False(shot.IsBallDetected);
        Assert.False(shot.HasPlayableBallMetrics);
    }

    [Fact]
    public void GarminLaunchDirection_MapsThroughConnectBallMapper()
    {
        var json = File.ReadAllText(FixturePath("connect-logs", "garmin-full-shot.json"));
        var shot = new GarminConnectBallMapper().TryMapJson(json);
        Assert.NotNull(shot);
        Assert.Equal(1.6, shot!.BallData!.Hla);
        Assert.Equal(ShotDirection.Right, ShotEffectMapper.ClassifyDirection(shot.BallData.Hla, 1.5));
    }

    private static string FixturePath(string folder, string fileName) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "fixtures", folder, fileName));
}
