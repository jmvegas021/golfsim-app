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
        var isPutt = shot.IsPutting == true ||
                     (shot.SpinType?.Contains("putt", StringComparison.OrdinalIgnoreCase) ?? false);
        var direction = ShotEffectMapper.ClassifyDirection(shot.BallData?.Hla, centerHlaAbsDegrees: 1.5);
        var entry = new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = isPutt ? "Putt" : "Shot",
            ShotNumber = shot.ShotNumber,
            BallSpeed = shot.BallData?.Speed,
            Hla = shot.BallData?.Hla,
            SpinAxis = shot.BallData?.SpinAxis,
            Carry = shot.BallData?.CarryDistance,
            Smash = shot.SmashFactor,
            Summary = BuildShotSummary(shot, isPutt, direction)
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
            Summary = "R50 ready"
        });
        return Task.CompletedTask;
    }

    public Task OnBallNotReadyAsync(CancellationToken cancellationToken = default)
    {
        Add(new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = "Not ready",
            Summary = "R50 not ready"
        });
        return Task.CompletedTask;
    }

    public Task OnWaitingAsync(CancellationToken cancellationToken = default)
    {
        Add(new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = "Waiting",
            Summary = "GSPro / Connect loading (WLED Ripple)"
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Surfaces a sparse Connect/R50 watch diagnostic line (errors / connect status).
    /// </summary>
    public void AddRaw(string kind, string summary)
    {
        Add(new ShotFeedEntry
        {
            Timestamp = DateTimeOffset.Now,
            Kind = kind,
            Summary = Truncate(summary, 220)
        });
    }

    private static string BuildShotSummary(ShotPayload shot, bool isPutt, ShotDirection direction)
    {
        var ball = shot.BallData;
        var parts = new List<string>();
        if (isPutt)
            parts.Add("Putt");
        parts.Add(direction switch
        {
            ShotDirection.Left => "Left",
            ShotDirection.Right => "Right",
            _ => "Center"
        });
        if (shot.ShotNumber is int n)
            parts.Add($"#{n}");
        if (ball?.CarryDistance is double carry)
            parts.Add($"carry {carry:F0} yd");
        if (ball?.Speed is double speed)
            parts.Add($"{speed:F1} mph");
        if (ball?.Hla is double hla)
            parts.Add($"HLA {hla:F1}°");
        if (ball?.SideSpin is double side)
            parts.Add($"sidespin {side:F0}");
        else if (ball?.SpinAxis is double axis)
            parts.Add($"axis {axis:F1}°");
        if (shot.SmashFactor is double smash)
            parts.Add($"smash {smash:F2}");
        if (!string.IsNullOrWhiteSpace(shot.SpinType) && !isPutt)
            parts.Add(shot.SpinType!);

        return parts.Count > 0 ? string.Join("  ", parts) : "Ball metrics";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

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
