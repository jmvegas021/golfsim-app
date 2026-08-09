using System.Net;
using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledNetworkDiscoveryTests
{
    [Fact]
    public void BuildCandidateAddresses_Slash24_Returns254HostsExcludingNetworkAndBroadcast()
    {
        var subnet = (IPAddress.Parse("192.168.1.50"), IPAddress.Parse("255.255.255.0"));

        var candidates = WledNetworkDiscovery.BuildCandidateAddresses(subnet).ToList();

        Assert.Equal(254, candidates.Count);
        Assert.DoesNotContain("192.168.1.0", candidates);
        Assert.DoesNotContain("192.168.1.255", candidates);
        Assert.Contains("192.168.1.1", candidates);
        Assert.Contains("192.168.1.254", candidates);
    }

    [Fact]
    public void BuildCandidateAddresses_Slash25_Returns126HostsWithinCorrectHalf()
    {
        // 10.0.0.50 on a /25 falls in the 10.0.0.0-10.0.0.127 half.
        var subnet = (IPAddress.Parse("10.0.0.50"), IPAddress.Parse("255.255.255.128"));

        var candidates = WledNetworkDiscovery.BuildCandidateAddresses(subnet).ToList();

        Assert.Equal(126, candidates.Count);
        Assert.Contains("10.0.0.1", candidates);
        Assert.Contains("10.0.0.126", candidates);
        Assert.DoesNotContain("10.0.0.0", candidates);
        Assert.DoesNotContain("10.0.0.127", candidates);
        Assert.DoesNotContain("10.0.0.128", candidates);
    }

    [Fact]
    public void BuildCandidateAddresses_LargerThanSlash24_YieldsNothing()
    {
        // /16 (65k+ hosts) is deliberately skipped to keep a scan bounded.
        var subnet = (IPAddress.Parse("172.16.5.5"), IPAddress.Parse("255.255.0.0"));

        var candidates = WledNetworkDiscovery.BuildCandidateAddresses(subnet).ToList();

        Assert.Empty(candidates);
    }
}
