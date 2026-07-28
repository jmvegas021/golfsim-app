using GsproLighting.Gspro.Parsing;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ConnectLogLineParserTests
{
    [Fact]
    public void ReadyForShot_EmitsReady()
    {
        var parser = new ConnectLogLineParser();
        var result = parser.Parse("GarminR50Form: readyForShot=true ballPlacement ready");
        Assert.Equal(ConnectParseKind.Ready, result.Kind);
    }

    [Fact]
    public void ReadyForShot_KeepaliveRepeats_AreIgnored()
    {
        var parser = new ConnectLogLineParser();
        const string line = "GarminR50Form: readyForShot=true ballPlacement ready";
        Assert.Equal(ConnectParseKind.Ready, parser.Parse(line).Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse(line).Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("Sent readyForShot READY_TO_HIT").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse(line).Kind);
    }

    [Fact]
    public void ReadyForShot_ReEmitsAfterNotReady()
    {
        var parser = new ConnectLogLineParser();
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("NOT_READY_TO_HIT").Kind);
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("readyForShot=true").Kind);
    }

    [Fact]
    public void ReadyForShot_False_ClearsReadyWithoutEmitting()
    {
        var parser = new ConnectLogLineParser();
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("readyForShot=false").Kind);
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);
    }

    [Fact]
    public void ReadyForShot_ReEmitsAfterShot()
    {
        var parser = new ConnectLogLineParser();
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);

        var json = File.ReadAllText(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "connect-logs", "garmin-full-shot.json")));
        var shotLine = "Logging ball data IMMEDIATELY before sending to GSPro " +
                       json.Replace("\n", " ").Replace("\r", "");
        Assert.Equal(ConnectParseKind.Shot, parser.Parse(shotLine).Kind);
        Assert.Equal(ConnectParseKind.Ready, parser.Parse("readyForShot=true").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("readyForShot=true").Kind);
    }

    [Fact]
    public void BallMarker_EmitsRawWhenJsonMissing()
    {
        var parser = new ConnectLogLineParser();
        var result = parser.Parse("Logging ball data IMMEDIATELY before sending to GSPro");
        Assert.Equal(ConnectParseKind.Raw, result.Kind);
        Assert.Contains("Logging ball data", result.RawLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultilineBallJson_EmitsShot()
    {
        var parser = new ConnectLogLineParser();
        Assert.Equal(ConnectParseKind.Raw, parser.Parse("Logging ball data IMMEDIATELY before sending to GSPro").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("{").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"ballSpeed\": 148.2,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"launchAngle\": 12.4,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"launchDirection\": 1.6,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"carryDistance\": 246.0,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"sidespin\": -420.0,").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"spinType\": \"normal\",").Kind);
        Assert.Equal(ConnectParseKind.Ignore, parser.Parse("  \"shotNumber\": 12").Kind);

        var shot = parser.Parse("}");
        Assert.Equal(ConnectParseKind.Shot, shot.Kind);
        Assert.NotNull(shot.Shot);
        Assert.Equal(246.0, shot.Shot!.BallData!.CarryDistance);
        Assert.Equal(148.2, shot.Shot.BallData.Speed);
        Assert.Equal(-420.0, shot.Shot.BallData.SideSpin);
    }

    [Fact]
    public void SameLineBallJson_EmitsShot()
    {
        var parser = new ConnectLogLineParser();
        var json = File.ReadAllText(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "connect-logs", "garmin-full-shot.json")));
        var line = "Logging ball data IMMEDIATELY before sending to GSPro " + json.Replace("\n", " ").Replace("\r", "");
        var result = parser.Parse(line);
        Assert.Equal(ConnectParseKind.Shot, result.Kind);
        Assert.Equal(246.0, result.Shot!.BallData!.CarryDistance);
    }

    [Fact]
    public void MultilinePuttJson_EmitsPuttingShot()
    {
        var parser = new ConnectLogLineParser();
        parser.Parse("Logging ball data IMMEDIATELY before sending to GSPro");
        var putt = File.ReadAllText(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "connect-logs", "garmin-putt.json")));
        ConnectParseResult? last = null;
        foreach (var line in putt.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var parsed = parser.Parse(line);
            if (parsed.Kind != ConnectParseKind.Ignore)
                last = parsed;
        }

        Assert.NotNull(last);
        Assert.Equal(ConnectParseKind.Shot, last!.Kind);
        Assert.True(last.Shot!.IsPutting == true ||
                    last.Shot.SpinType?.Contains("putt", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal(8.0, last.Shot.BallData!.CarryDistance);
    }
}
