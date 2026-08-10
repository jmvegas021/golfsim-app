using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Parsing;
using Xunit;

namespace GsproLighting.Tests;

/// <summary>
/// Matches the v0.8.26 R50 export shape: property fragments before the ball marker,
/// then Force BallSpeed — no brace-wrapped JSON after the marker.
/// </summary>
public sealed class GarminSparseShotLogTests
{
    [Fact]
    public void PreMarkerPropertiesPlusForceLine_EmitsDirectionalShot()
    {
        var parser = new ConnectLogLineParser();

        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("      \"sidespin\": -734,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("      \"spinType\": \"MEASURED\"").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("      \"carryDistance\": 11.351880073547363,").Kind);
        Assert.Equal(
            ConnectParseKind.Ignore,
            parser.Parse("      \"carryDeviationDistance\": 1.8679461479187012,").Kind);
        Assert.Equal(
            ConnectParseKind.Ignore,
            parser.Parse("      \"carryDeviationAngle\": 0.1653008608988347,").Kind);

        Assert.Equal(
            ConnectParseKind.Raw,
            parser.Parse("Logging ball data IMMEDIATELY before sending to GSPro").Kind);

        var shot = parser.Parse(
            "Force 11.57|| SurfMul 0.72|| BallSpeed 22.77|| AngleSpeedBounce 0.42|| descent angle 38.2|| ExitAngle 36.9");

        Assert.Equal(ConnectParseKind.Shot, shot.Kind);
        Assert.NotNull(shot.Shot);
        Assert.Equal(22.77, shot.Shot!.BallData!.Speed);
        Assert.Equal(11.351880073547363, shot.Shot.BallData.CarryDistance);
        Assert.InRange(shot.Shot.BallData.Hla!.Value, 9.0, 10.0);
        Assert.Equal(36.9, shot.Shot.BallData.Vla);
        Assert.False(shot.Shot.IsPutting == true);
        Assert.Equal(
            ShotDirection.Right,
            ShotEffectMapper.ClassifyDirection(shot.Shot.BallData.Hla, 1.5));
    }

    [Fact]
    public void ConnectedToLm_EmitsWaiting()
    {
        var parser = new ConnectLogLineParser();
        var waiting = parser.Parse(
            " ==> Connected to LM! Telling it we're ready for shot ...");
        Assert.Equal(ConnectParseKind.Waiting, waiting.Kind);

        // Fresh LM connect re-arms Waiting (bay reload / reconnect).
        Assert.Equal(
            ConnectParseKind.Waiting,
            parser.Parse(" ==> Connected to LM! Telling it we're ready for shot ...").Kind);
    }

    [Fact]
    public void LookingForGarmin_EmitsWaitingOnceBeforeReady()
    {
        var parser = new ConnectLogLineParser();
        Assert.Equal(
            ConnectParseKind.Waiting,
            parser.Parse("Looking for Garmin ...").Kind);
        Assert.Equal(
            ConnectParseKind.Ignore,
            parser.Parse("Looking for Garmin ...").Kind);
    }

    [Fact]
    public void ForceLineAlone_ParsesBallSpeed()
    {
        var shot = ConnectLogKeyValueMapper.TryMap(
            "Force 12.80|| SurfMul 0.49|| BallSpeed 24.50|| ExitAngle 40.0",
            context: null);
        Assert.NotNull(shot);
        Assert.Equal(24.50, shot!.BallData!.Speed);
        Assert.Equal(40.0, shot.BallData.Vla);
    }

    [Fact]
    public void DirectionBands_MatchReadyHoldWidth()
    {
        const int ledCount = 585;
        var ready = Wled.Animations.DrgbConcentrateBandGeometry.ResolveCenter(ledCount);
        var left = Wled.Animations.DrgbConcentrateBandGeometry.ResolveLeft(ledCount);
        var right = Wled.Animations.DrgbConcentrateBandGeometry.ResolveRight(ledCount);

        Assert.Equal(ready.LitCount, left.LitCount);
        Assert.Equal(ready.LitCount, right.LitCount);
        Assert.Equal(ready.Start, left.EndExclusive);
        Assert.Equal(ready.EndExclusive, right.Start);
    }
}
