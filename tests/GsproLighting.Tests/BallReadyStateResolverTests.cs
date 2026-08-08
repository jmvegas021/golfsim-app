using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using Xunit;

namespace GsproLighting.Tests;

public sealed class BallReadyStateResolverTests
{
    [Fact]
    public void Resolve_NoReadySignal_ReturnsUnknown()
    {
        var entries = new[] { Entry("NET") };

        var state = new BallReadyStateResolver().Resolve(entries);

        Assert.Equal(BallReadyState.Unknown, state);
    }

    [Fact]
    public void Resolve_ReturnsNewestObservedReadyState()
    {
        var entries = new[] { Entry("Shot"), Entry("Not ready"), Entry("Ready") };

        var state = new BallReadyStateResolver().Resolve(entries);

        Assert.Equal(BallReadyState.NotReady, state);
    }

    private static ShotFeedEntry Entry(string kind) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Kind = kind
    };
}
