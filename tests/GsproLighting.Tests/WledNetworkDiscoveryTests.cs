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
    public void BuildCandidateAddresses_WiderThanSlash24_ClampsToHostSlash24()
    {
        // /16 would previously yield nothing — clamp to the /24 containing the host.
        var subnet = (IPAddress.Parse("172.16.5.5"), IPAddress.Parse("255.255.0.0"));

        var candidates = WledNetworkDiscovery.BuildCandidateAddresses(subnet).ToList();

        Assert.Equal(254, candidates.Count);
        Assert.Contains("172.16.5.1", candidates);
        Assert.Contains("172.16.5.254", candidates);
        Assert.DoesNotContain("172.16.5.0", candidates);
        Assert.DoesNotContain("172.16.4.1", candidates);
        Assert.DoesNotContain("172.16.6.1", candidates);
    }

    [Fact]
    public void BuildCandidateAddresses_LinkLocal_YieldsNothing()
    {
        var subnet = (IPAddress.Parse("169.254.10.20"), IPAddress.Parse("255.255.0.0"));

        var candidates = WledNetworkDiscovery.BuildCandidateAddresses(subnet).ToList();

        Assert.Empty(candidates);
    }
}
