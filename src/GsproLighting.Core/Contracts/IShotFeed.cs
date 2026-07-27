using GsproLighting.Core.Models;

namespace GsproLighting.Core.Contracts;

public interface IShotFeed
{
    event Action<ShotFeedEntry>? EntryAdded;
    IReadOnlyList<ShotFeedEntry> Recent { get; }
    void Clear();
}
