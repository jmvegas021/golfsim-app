using GsproLighting.Core.Config;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledConfigConfiguredTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("192.168.1.50", false)]
    [InlineData("192.168.86.89", true)]
    [InlineData("wled.local", true)]
    public void IsConfiguredController_TreatsPlaceholderAndBlankAsUnconfigured(
        string? ip,
        bool expected) =>
        Assert.Equal(expected, WledConfig.IsConfiguredController(ip));
}
