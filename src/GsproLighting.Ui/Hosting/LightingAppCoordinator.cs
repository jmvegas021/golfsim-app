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
    private CancellationTokenSource? _proxyCts;
    private Task? _proxyTask;

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
    public bool IsProxyRunning => _proxyTask is { IsCompleted: false };
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
        if (IsProxyRunning)
            return;

        _proxyCts = new CancellationTokenSource();
        var sink = new CompositeShotEventSink(
            _feed,
            new ConsoleShotEventSink());

        var proxy = new GsproConnectProxy(
            Config.Gspro,
            new GsproMessageParser(),
            new FileRawMessageLogger(Config.Logging.RawLogDirectory, Config.Logging.LogHeartbeats),
            sink,
            Config.Logging.LogHeartbeats);

        _proxyTask = Task.Run(async () =>
        {
            try
            {
                await proxy.RunAsync(_proxyCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                ProxyStateChanged?.Invoke();
            }
        });

        ProxyStateChanged?.Invoke();
    }

    public async Task StopProxyAsync()
    {
        if (_proxyCts is null)
            return;

        _proxyCts.Cancel();
        if (_proxyTask is not null)
        {
            try
            {
                await _proxyTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _proxyCts.Dispose();
        _proxyCts = null;
        _proxyTask = null;
        ProxyStateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await StopProxyAsync();
        await _wled.DisposeAsync();
    }
}
