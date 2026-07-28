using GsproLighting.Gspro.Parsing;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ConnectReadySignalClassifierTests
{
    [Theory]
    [InlineData("\"status\": \"NOT_READY_TO_HIT\", type: ballPlacement")]
    [InlineData("NOT_READY_TO_HIT")]
    [InlineData("readyForShot=false")]
    [InlineData("readyForShot: false")]
    [InlineData("ReadyForShot\":false")]
    [InlineData("LaunchMonitorIsReady\": false")]
    public void NotReadyTokens_Match(string line)
    {
        Assert.True(ConnectReadySignalClassifier.IsNotReady(line));
        Assert.False(ConnectReadySignalClassifier.IsReady(line));
    }

    [Theory]
    [InlineData("READY_TO_HIT")]
    [InlineData("Sent readyForShot READY_TO_HIT")]
    [InlineData("readyForShot=true")]
    [InlineData("readyForShot: true")]
    [InlineData("ReadyForShot\":true")]
    [InlineData("LaunchMonitorIsReady\":true")]
    public void ReadyTokens_Match(string line)
    {
        Assert.True(ConnectReadySignalClassifier.IsReady(line));
        Assert.False(ConnectReadySignalClassifier.IsNotReady(line));
    }

    [Theory]
    [InlineData("readyForShot")]
    [InlineData("Sent readyForShot")]
    [InlineData("GarminR50Form keepAlive")]
    [InlineData("type: ballPlacement")]
    public void AmbiguousOrNoise_IsNeither(string line)
    {
        Assert.False(ConnectReadySignalClassifier.IsReady(line));
        Assert.False(ConnectReadySignalClassifier.IsNotReady(line));
    }
}
