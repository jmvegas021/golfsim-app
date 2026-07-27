using System.Diagnostics;
using System.Text.Json;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Discovery;

namespace GsproLighting.Gspro.Watchers;

/// <summary>
/// Watches discovered Connect↔R50 TCP peers; logs metadata (and optional pktmon if available).
/// </summary>
public sealed class R50NetworkWatcher : IAsyncDisposable
{
    private readonly ShotFeedBuffer _feed;
    private readonly string _rawLogDirectory;
    private readonly object _gate = new();
    private readonly HashSet<string> _seenPeers = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _payloadLimitedNotified;
    private string? _lastEvent;
    private IReadOnlyList<DiscoveredPeerEndpoint> _peers = Array.Empty<DiscoveredPeerEndpoint>();

    public R50NetworkWatcher(ShotFeedBuffer feed, string rawLogDirectory)
    {
        _feed = feed;
        _rawLogDirectory = rawLogDirectory;
    }

    public string? LastEvent => _lastEvent;
    public bool PayloadCaptureLimited { get; private set; } = true;
    public IReadOnlyList<DiscoveredPeerEndpoint> Peers => _peers;

    public void Start()
    {
        lock (_gate)
        {
            if (_loop is { IsCompleted: false })
                return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public void UpdatePeers(IReadOnlyList<DiscoveredPeerEndpoint> peers)
    {
        _peers = peers;
        foreach (var peer in peers)
        {
            var key = $"{peer.RemoteAddress}:{peer.RemotePort}";
            if (!_seenPeers.Add(key))
                continue;

            _lastEvent = $"peer {key}";
            _feed.AddRaw("NET", $"R50 peer connected {peer.Display}");
            AppendJson(new
            {
                ts = DateTimeOffset.UtcNow,
                kind = "peer",
                peer.RemoteAddress,
                peer.RemotePort,
                peer.LocalAddress,
                peer.LocalPort,
                peer.ProcessName,
                peer.ProcessId,
                peer.State
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
        }

        if (cts is null)
            return;

        cts.Cancel();
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        cts.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        await TryProbePktmonAsync(token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            foreach (var peer in _peers)
            {
                AppendJson(new
                {
                    ts = DateTimeOffset.UtcNow,
                    kind = "heartbeat",
                    peer = peer.Display,
                    peer.State
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
        }
    }

    private async Task TryProbePktmonAsync(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "pktmon",
                Arguments = "status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
                return;

            var output = await process.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(token).ConfigureAwait(false);
            await process.WaitForExitAsync(token).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                PayloadCaptureLimited = false;
                _lastEvent = "pktmon available";
                AppendJson(new { ts = DateTimeOffset.UtcNow, kind = "pktmon", output, error });
            }
            else
            {
                NotifyLimitedOnce();
            }
        }
        catch
        {
            NotifyLimitedOnce();
        }
    }

    private void NotifyLimitedOnce()
    {
        PayloadCaptureLimited = true;
        if (_payloadLimitedNotified)
            return;
        _payloadLimitedNotified = true;
        _feed.AddRaw(
            "NET",
            "network payload capture limited — run as admin for full sniff; log watch continues");
    }

    private void AppendJson(object payload)
    {
        try
        {
            Directory.CreateDirectory(_rawLogDirectory);
            var path = Path.Combine(_rawLogDirectory, $"r50-net-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
            lock (_gate)
                File.AppendAllText(path, line);
        }
        catch
        {
            // Non-fatal.
        }
    }
}
