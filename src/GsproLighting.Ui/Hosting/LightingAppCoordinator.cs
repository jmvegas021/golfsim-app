using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Logging;
using GsproLighting.Gspro.Parsing;
using GsproLighting.Gspro.Proxy;
using GsproLighting.Wled;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Ui.Hosting;

/// <summary>
/// Owns proxy lifetime, config, WLED output, and the live shot feed.
/// </summary>
public sealed class LightingAppCoordinator : IAsyncDisposable
{
    private readonly ConfigStore _store;
    private readonly ShotFeedBuffer _feed = new();
    private readonly WarlsWledOutput _wled = new();
    private readonly object _proxyGate = new();
    private CancellationTokenSource? _proxyCts;
    private Task? _proxyTask;
    private string? _lastProxyError;

    public LightingAppCoordinator(ConfigStore store)
    {
        _store = store;
        Config = store.Load();
        _wled.Configure(Config.Wled);
        Preview = new WledPreviewPlayer(_wled);
    }

    public AppConfig Config { get; private set; }
    public IShotFeed Feed => _feed;
    public IWledOutput Wled => _wled;
    public WledPreviewPlayer Preview { get; }
    public string? LastProxyError => _lastProxyError;

    public bool IsProxyRunning
    {
        get
        {
            lock (_proxyGate)
                return _proxyTask is { IsCompleted: false };
        }
    }

    public event Action? ProxyStateChanged;

    public void SaveConfig(AppConfig config)
    {
        Config = config;
        _store.Save(config);
        _wled.Configure(config.Wled);
    }

    public void ReloadConfig()
    {
        Config = _store.Load();
        _wled.Configure(Config.Wled);
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

            var sink = new CompositeShotEventSink(_feed);
            var proxy = new GsproConnectProxy(
                Config.Gspro,
                new GsproMessageParser(),
                new FileRawMessageLogger(Config.Logging.RawLogDirectory, Config.Logging.LogHeartbeats),
                sink,
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

    public async ValueTask DisposeAsync()
    {
        await StopProxyAsync().ConfigureAwait(false);
        await _wled.DisposeAsync().ConfigureAwait(false);
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
}
