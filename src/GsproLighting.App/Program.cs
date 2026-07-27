using System.Text.Json;
using GsproLighting.Core.Config;
using GsproLighting.Gspro.Logging;
using GsproLighting.Gspro.Parsing;
using GsproLighting.Gspro.Proxy;
using GsproLighting.Gspro.Simulation;

namespace GsproLighting.App;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var mode = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "help";
        var config = LoadConfig();
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        return mode switch
        {
            "proxy" => await RunProxyAsync(config, cts.Token),
            "mock" => await RunMockAsync(config, cts.Token),
            "replay" => await RunReplayAsync(config, args, cts.Token),
            "help" or "--help" or "-h" => PrintHelp(),
            _ => UnknownMode(mode)
        };
    }

    private static async Task<int> RunProxyAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var proxy = CreateProxy(config);
        await proxy.RunAsync(cancellationToken);
        return 0;
    }

    private static async Task<int> RunMockAsync(AppConfig config, CancellationToken cancellationToken)
    {
        // Prefer a high port for the offline mock so fixture tests don't collide with real GSPro :921.
        var port = config.Gspro.UpstreamPort < 1024 ? 9921 : config.Gspro.UpstreamPort;
        var server = new MockGsproServer(config.Gspro.UpstreamHost, port);
        await server.RunAsync(cancellationToken);
        return 0;
    }

    private static async Task<int> RunReplayAsync(
        AppConfig config,
        string[] args,
        CancellationToken cancellationToken)
    {
        var fixturesDir = ResolveFixturesDirectory(GetOption(args, "--fixtures"));

        // Real GSPro uses 921; offline replay uses 9921 so it won't fight a running GSPro.
        var replayConfig = CloneConfig(config);
        replayConfig.Gspro.UpstreamPort = 9921;

        Console.WriteLine("[replay] starting mock GSPro + proxy + fixture injector");
        Console.WriteLine($"[replay] fixtures: {fixturesDir}");
        Console.WriteLine(
            $"[replay] LM → :{replayConfig.Gspro.ListenPort} → mock GSPro :{replayConfig.Gspro.UpstreamPort}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var mockTask = new MockGsproServer(
            replayConfig.Gspro.UpstreamHost,
            replayConfig.Gspro.UpstreamPort).RunAsync(token);
        await Task.Delay(300, token);

        var proxyTask = CreateProxy(replayConfig).RunAsync(token);
        await Task.Delay(300, token);

        try
        {
            var injector = new FixtureShotInjector(
                replayConfig.Gspro.ListenHost,
                replayConfig.Gspro.ListenPort);
            await injector.InjectDirectoryAsync(fixturesDir, TimeSpan.FromMilliseconds(350), token);
            await Task.Delay(800, token);
        }
        finally
        {
            linked.Cancel();
        }

        try
        {
            await Task.WhenAll(mockTask, proxyTask);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine("[replay] done — check logs/gspro-raw-*.jsonl for captured traffic");
        return 0;
    }

    private static AppConfig CloneConfig(AppConfig source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private static GsproConnectProxy CreateProxy(AppConfig config)
    {
        return new GsproConnectProxy(
            config.Gspro,
            new GsproMessageParser(),
            new FileRawMessageLogger(config.Logging.RawLogDirectory, config.Logging.LogHeartbeats),
            new ConsoleShotEventSink(),
            config.Logging.LogHeartbeats);
    }

    private static string ResolveFixturesDirectory(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath))
            return Path.GetFullPath(overridePath);

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "fixtures", "shots"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "shots")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures", "shots"))
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
                return path;
        }

        throw new DirectoryNotFoundException(
            "Could not find fixtures/shots. Pass --fixtures <dir>.");
    }

    private static AppConfig LoadConfig()
    {
        var store = new ConfigStore();
        Console.WriteLine($"[config] loaded {store.Path}");
        return store.Load();
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            GSPro Reactive Lighting — Windows console tools

            Usage:
              GsproLighting.App.exe <mode> [options]

            Modes:
              proxy   Listen for LM traffic, forward to GSPro, log+parse both directions
              mock    Run a fake GSPro Open Connect server (offline fixture testing)
              replay  mock + proxy + inject fixtures\shots\*.json (no GSPro/LM required)

            Options:
              --fixtures <dir>   Fixture directory for replay mode

            Windows / Ally setup (proxy mode):
              1. Start GSPro (Open Connect on 127.0.0.1:921)
              2. Run this app in proxy mode (listens on 1921 by default)
                 Prefer GsproLighting.exe (tray UI) for day-to-day use
              3. Point your LM / Garmin→GSPro bridge at 127.0.0.1:1921
              4. Hit water/OB/putts and inspect logs\gspro-raw-*.jsonl for unknown fields
            """);
        return 0;
    }

    private static int UnknownMode(string mode)
    {
        Console.WriteLine($"Unknown mode '{mode}'. Run with 'help'.");
        return 1;
    }
}
