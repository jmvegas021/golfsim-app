using GsproLighting.Core.Contracts;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Discovery;

namespace GsproLighting.Gspro.Watchers;

/// <summary>
/// Orchestrates periodic Connect discovery plus log/network watchers.
/// </summary>
public sealed class R50AutoWatchManager : IAsyncDisposable
{
    private readonly ConnectLogDiscoverer _logDiscoverer = new();
    private readonly ConnectPeerDiscoverer _peerDiscoverer = new();
    private readonly ConnectLogTailWatcher _logWatcher;
    private readonly R50NetworkWatcher _networkWatcher;
    private readonly int _refreshSeconds;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private ConnectDiscoverySnapshot _snapshot = new();
    private bool _running;

    public R50AutoWatchManager(
        ShotFeedBuffer feed,
        IShotEventSink sink,
        string rawLogDirectory,
        int refreshSeconds = 10)
    {
        _refreshSeconds = Math.Clamp(refreshSeconds, 5, 60);
        _logWatcher = new ConnectLogTailWatcher(feed, sink, rawLogDirectory);
        _networkWatcher = new R50NetworkWatcher(feed, rawLogDirectory);
    }

    public ConnectDiscoverySnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    public bool IsRunning => _running;
    public string? LastLogLine => _logWatcher.LastRawLine;
    public string? LastNetworkEvent => _networkWatcher.LastEvent;

    public event Action? StatusChanged;

    public void Start()
    {
        lock (_gate)
        {
            if (_loop is { IsCompleted: false })
                return;
            _running = true;
            _cts = new CancellationTokenSource();
            _logWatcher.Start();
            _networkWatcher.Start();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }

        RaiseStatusChanged();
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            _running = false;
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
        }

        if (cts is not null)
        {
            cts.Cancel();
            if (loop is not null)
            {
                try { await loop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            cts.Dispose();
        }

        await _logWatcher.DisposeAsync().ConfigureAwait(false);
        await _networkWatcher.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            RefreshOnce();
            await Task.Delay(TimeSpan.FromSeconds(_refreshSeconds), token).ConfigureAwait(false);
        }
    }

    private void RefreshOnce()
    {
        try
        {
            var logs = _logDiscoverer.Discover();
            var (names, peers) = _peerDiscoverer.Discover();
            _logWatcher.UpdateWatchedFiles(logs.Select(l => l.FullPath));
            _networkWatcher.UpdatePeers(peers);

            var snapshot = new ConnectDiscoverySnapshot
            {
                TakenAt = DateTimeOffset.Now,
                LogFiles = logs,
                Peers = peers,
                ConnectProcessNames = names,
                NetworkPayloadCaptureLimited = _networkWatcher.PayloadCaptureLimited
            };

            lock (_gate)
                _snapshot = snapshot;

            RaiseStatusChanged();
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _snapshot = new ConnectDiscoverySnapshot
                {
                    TakenAt = DateTimeOffset.Now,
                    Error = $"Discovery error: {ex.Message}",
                    NetworkPayloadCaptureLimited = _networkWatcher.PayloadCaptureLimited
                };
            }

            RaiseStatusChanged();
        }
    }

    private void RaiseStatusChanged()
    {
        try
        {
            StatusChanged?.Invoke();
        }
        catch
        {
            // UI subscribers must not break the watch loop.
        }
    }
}
