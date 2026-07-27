using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Core.Services;

public sealed class ShotFeedBuffer : IShotFeed, IShotEventSink
{
    private readonly object _gate = new();
    private readonly List<ShotFeedEntry> _entries = new();
    private readonly int _capacity;

    public ShotFeedBuffer(int capacity = 50)
    {
        _capacity = capacity;
    }

    public event Action<ShotFeedEntry>? EntryAdded;

    public IReadOnlyList<ShotFeedEntry> Recent
    {
        get
        {
            lock (_gate)
                return _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
    }

    public Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        var entry = new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = "Shot",
            ShotNumber = shot.ShotNumber,
            BallSpeed = shot.BallData?.Speed,
            Hla = shot.BallData?.Hla,
            SpinAxis = shot.BallData?.SpinAxis,
            Carry = shot.BallData?.CarryDistance,
            Smash = shot.SmashFactor,
            Summary =
                $"#{shot.ShotNumber}  {shot.BallData?.Speed:F1} mph  " +
                $"HLA {shot.BallData?.Hla:F1}°  carry {shot.BallData?.CarryDistance:F0} yd" +
                (shot.SmashFactor is double s ? $"  smash {s:F2}" : string.Empty)
        };
        Add(entry);
        return Task.CompletedTask;
    }

    public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        var label = response.Code switch
        {
            200 => "Ack",
            201 => "Player",
            _ => $"Code {response.Code}"
        };

        Add(new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = label,
            Summary = $"{label}: {response.Player?.Club ?? "—"} / {response.Player?.Handed ?? "—"}  {response.Message}"
        });
        return Task.CompletedTask;
    }

    public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        Add(new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = "Ready",
            Summary = "Ball detected — ready"
        });
        return Task.CompletedTask;
    }

    private void Add(ShotFeedEntry entry)
    {
        lock (_gate)
        {
            _entries.Insert(0, entry);
            while (_entries.Count > _capacity)
                _entries.RemoveAt(_entries.Count - 1);
        }

        EntryAdded?.Invoke(entry);
    }
}
