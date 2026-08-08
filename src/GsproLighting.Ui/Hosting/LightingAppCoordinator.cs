using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Discovery;
using GsproLighting.Gspro.Logging;
using GsproLighting.Gspro.Parsing;
using GsproLighting.Gspro.Proxy;
using GsproLighting.Gspro.Watchers;
using GsproLighting.Wled;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Ui.Hosting;

/// <summary>
/// Owns proxy lifetime, R50 auto-watch, config, WLED output, and the live shot feed.
/// </summary>
public sealed class LightingAppCoordinator : IAsyncDisposable
{
    private readonly ConfigStore _store;
    private readonly ShotFeedBuffer _feed = new();
    private readonly BallReadyStateResolver _readyStateResolver = new();
    private readonly WarlsWledOutput _wled = new();
    private readonly WledShotEffectSink _effectSink;
    private readonly CompositeShotEventSink _shotSink;
    private readonly object _proxyGate = new();
    private CancellationTokenSource? _proxyCts;
    private Task? _proxyTask;
    private string? _lastProxyError;
    private R50AutoWatchManager? _r50Watch;
    private bool _firstShotBalloonShown;

    public LightingAppCoordinator(ConfigStore store)
    {
        _store = store;
        Config = store.Load();
        NormalizePaths(Config);
        _wled.Configure(Config.Wled);
        Preview = new WledPreviewPlayer(_wled);
        _effectSink = new WledShotEffectSink(
            _wled,
            () => Config.Effects,
            () => Config.Wled,
            logFailure: msg => _feed.AddRaw("WLED", msg));
        _shotSink = new CompositeShotEventSink(_feed, _effectSink);
    }

    public AppConfig Config { get; private set; }
    public IShotFeed Feed => _feed;
    public ShotFeedBuffer FeedBuffer => _feed;
    public IWledOutput Wled => _wled;
    public WledPreviewPlayer Preview { get; }
    public string? LastProxyError => _lastProxyError;
    public ConnectDiscoverySnapshot? R50Snapshot => _r50Watch?.Snapshot;
    public bool IsR50WatchRunning => _r50Watch?.IsRunning == true;
    public string? LastR50LogLine => _r50Watch?.LastLogLine;
    public string? LastR50NetworkEvent => _r50Watch?.LastNetworkEvent;
    public BallReadyState BallReadyState => _readyStateResolver.Resolve(_feed.Recent);

    public bool IsProxyRunning
    {
        get
        {
            lock (_proxyGate)
                return _proxyTask is { IsCompleted: false };
        }
    }

    public event Action? ProxyStateChanged;
    public event Action? R50StatusChanged;
    public event Action? FirstShotObserved;

    public void SaveConfig(AppConfig config)
    {
        NormalizePaths(config);
        Config = config;
        _store.Save(config);
        _wled.Configure(config.Wled);
    }

    public void ReloadConfig()
    {
        Config = _store.Load();
        NormalizePaths(Config);
        _wled.Configure(Config.Wled);
    }

    public void StartR50AutoWatch()
    {
        if (!Config.R50Watch.AutoWatchEnabled)
            return;
        if (_r50Watch is not null)
            return;

        try
        {
            Directory.CreateDirectory(Config.Logging.RawLogDirectory);
        }
        catch (Exception ex)
        {
            CrashLog.Write("R50WatchLogs", ex);
        }

        _r50Watch = new R50AutoWatchManager(
            _feed,
            _shotSink,
            Config.Logging.RawLogDirectory,
            Config.R50Watch.DiscoveryRefreshSeconds);
        _r50Watch.StatusChanged += () =>
        {
            try { R50StatusChanged?.Invoke(); }
            catch (Exception ex) { CrashLog.Write("R50StatusChanged", ex); }
        };
        _feed.EntryAdded += OnFeedEntryForBalloon;
        _r50Watch.Start();
        RaiseR50StatusChanged();
    }

    public async Task StopR50AutoWatchAsync()
    {
        var watch = _r50Watch;
        _r50Watch = null;
        _feed.EntryAdded -= OnFeedEntryForBalloon;
        if (watch is null)
            return;
        await watch.DisposeAsync().ConfigureAwait(false);
        RaiseR50StatusChanged();
    }

    public void StartProxy()
    {
        lock (_proxyGate)
        {
            if (_proxyTask is { IsCompleted: false })
                return;

            _lastProxyError = null;
            _proxyCts = new CancellationTokenSource();
            var token = _proxyCts.Token;

            try
            {
                Directory.CreateDirectory(Config.Logging.RawLogDirectory);
            }
            catch (Exception ex)
            {
                _lastProxyError = $"Cannot create log folder: {ex.Message}";
                RaiseProxyStateChanged();
                return;
            }

            var proxy = new GsproConnectProxy(
                Config.Gspro,
                new GsproMessageParser(),
                new FileRawMessageLogger(Config.Logging.RawLogDirectory, Config.Logging.LogHeartbeats),
                _shotSink,
                Config.Logging.LogHeartbeats);

            _proxyTask = Task.Run(() => RunProxySafeAsync(proxy, token), token);
        }

        RaiseProxyStateChanged();
    }

    public async Task StopProxyAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_proxyGate)
        {
            cts = _proxyCts;
            task = _proxyTask;
            _proxyCts = null;
            _proxyTask = null;
        }

        if (cts is null)
            return;

        cts.Cancel();
        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        cts.Dispose();
        RaiseProxyStateChanged();
    }

    public string BuildStatusText()
    {
        var parts = new List<string>();
        if (IsR50WatchRunning)
        {
            var snap = R50Snapshot;
            parts.Add(snap?.StatusSummary ?? "R50 auto-watch on");
            if (!string.IsNullOrWhiteSpace(LastR50LogLine))
                parts.Add("last: " + Truncate(LastR50LogLine!, 60));
        }
        else if (Config.R50Watch.AutoWatchEnabled)
        {
            parts.Add("R50 auto-watch starting…");
        }

        if (!string.IsNullOrWhiteSpace(_lastProxyError))
            parts.Add($"Proxy error: {_lastProxyError}");
        else if (IsProxyRunning)
            parts.Add($"Open Connect proxy :{Config.Gspro.ListenPort}→:{Config.Gspro.UpstreamPort}");

        return parts.Count == 0 ? "Idle" : string.Join(" · ", parts);
    }

    public async ValueTask DisposeAsync()
    {
        await StopR50AutoWatchAsync().ConfigureAwait(false);
        await StopProxyAsync().ConfigureAwait(false);
        Preview.Dispose();
        await _wled.DisposeAsync().ConfigureAwait(false);
    }

    private void OnFeedEntryForBalloon(Core.Models.ShotFeedEntry entry)
    {
        if (_firstShotBalloonShown)
            return;
        if (entry.Kind is not ("Shot" or "Putt" or "LOG" or "NET" or "Ready" or "Not ready"))
            return;
        _firstShotBalloonShown = true;
        try
        {
            FirstShotObserved?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("FirstShotObserved", ex);
        }
    }

    private async Task RunProxySafeAsync(GsproConnectProxy proxy, CancellationToken token)
    {
        try
        {
            await proxy.RunAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _lastProxyError = ex.Message;
            CrashLog.Write("Proxy", ex);
        }
        finally
        {
            RaiseProxyStateChanged();
        }
    }

    private void RaiseProxyStateChanged()
    {
        try
        {
            ProxyStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ProxyStateChanged", ex);
        }
    }

    private void RaiseR50StatusChanged()
    {
        try
        {
            R50StatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("R50StatusChanged", ex);
        }
    }

    private static void NormalizePaths(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Logging.RawLogDirectory) ||
            config.Logging.RawLogDirectory is "logs")
            config.Logging.RawLogDirectory = AppPaths.LogsDirectory;
        else if (!Path.IsPathRooted(config.Logging.RawLogDirectory))
            config.Logging.RawLogDirectory = Path.Combine(AppPaths.InstallDirectory, config.Logging.RawLogDirectory);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
