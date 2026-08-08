using GsproLighting.Core.Models;

namespace GsproLighting.Core.Services;

public sealed class BallReadyStateResolver
{
    public BallReadyState Resolve(IEnumerable<ShotFeedEntry> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);

        foreach (var entry in newestFirst)
        {
            if (entry.Kind.Equals("Ready", StringComparison.OrdinalIgnoreCase))
                return BallReadyState.Ready;
            if (entry.Kind.Equals("Not ready", StringComparison.OrdinalIgnoreCase))
                return BallReadyState.NotReady;
        }

        return BallReadyState.Unknown;
    }
}
