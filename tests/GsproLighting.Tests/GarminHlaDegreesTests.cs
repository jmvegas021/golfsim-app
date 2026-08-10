using GsproLighting.Core.Services;
using GsproLighting.Gspro.Parsing;
using Xunit;

namespace GsproLighting.Tests;

public sealed class GarminHlaDegreesTests
{
    [Theory]
    [InlineData(0.03426496711544845, 1.5, true)]   // ~1.96° → Right
    [InlineData(0.1653008608988347, 1.5, true)]    // ~9.5° → Right
    [InlineData(-0.22923017602424131, 1.5, false)] // ~-13.1° → Left
    [InlineData(0.0025605693109420655, 1.5, null)] // ~0.15° → Center
    public void CarryDeviationRadians_MapsDirectionBuckets(
        double radians,
        double centerAbs,
        bool? expectRight)
    {
        var degrees = GarminHlaDegrees.FromCarryDeviationRadians(radians);
        var direction = ShotEffectMapper.ClassifyDirection(degrees, centerAbs);

        if (expectRight is null)
            Assert.Equal(Core.Models.ShotDirection.Center, direction);
        else if (expectRight.Value)
            Assert.Equal(Core.Models.ShotDirection.Right, direction);
        else
            Assert.Equal(Core.Models.ShotDirection.Left, direction);
    }

    [Fact]
    public void Normalize_ConvertsOnlyCarryDeviationAngleKey()
    {
        Assert.Equal(1.6, GarminHlaDegrees.Normalize(1.6, "launchDirection"));
        Assert.Equal(
            GarminHlaDegrees.FromCarryDeviationRadians(0.165),
            GarminHlaDegrees.Normalize(0.165, "carryDeviationAngle"));
    }

    [Fact]
    public void Mapper_PrefersLaunchDirectionDegreesOverCarryDeviationRadians()
    {
        const string json = """
            {
              "ballSpeed": 148.2,
              "launchDirection": 1.6,
              "carryDeviationAngle": 0.028,
              "carryDistance": 246.0
            }
            """;

        var shot = new GarminConnectBallMapper().TryMapJson(json);
        Assert.NotNull(shot);
        Assert.Equal(1.6, shot!.BallData!.Hla);
        Assert.Equal(
            Core.Models.ShotDirection.Right,
            ShotEffectMapper.ClassifyDirection(shot.BallData.Hla, 1.5));
    }

    [Fact]
    public void Mapper_ConvertsCarryDeviationRadiansWhenOnlyHlaSource()
    {
        const string json = """
            {
              "ballSpeed": 24.5,
              "carryDeviationAngle": 0.1653008608988347,
              "carryDistance": 11.35,
              "sidespin": -734
            }
            """;

        var shot = new GarminConnectBallMapper().TryMapJson(json);
        Assert.NotNull(shot);
        Assert.InRange(shot!.BallData!.Hla!.Value, 9.0, 10.0);
        Assert.Equal(
            Core.Models.ShotDirection.Right,
            ShotEffectMapper.ClassifyDirection(shot.BallData.Hla, 1.5));
    }
}
