using System.Net;
using System.Net.Sockets;
using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;
using GsproLighting.Gspro.Dispatch;
using GsproLighting.Gspro.Framing;
using GsproLighting.Gspro.Parsing;

namespace GsproLighting.Gspro.Proxy;

/// <summary>
/// Bidirectional TCP proxy between a launch monitor and GSPro Open Connect.
/// Taps both directions for the spike logger and shot event sink.
/// </summary>
public sealed class GsproConnectProxy
{
    private readonly GsproConfig _config;
    private readonly GsproMessageParser _parser;
    private readonly IRawMessageLogger _rawLogger;
    private readonly IShotEventSink _shotSink;
    private readonly bool _logHeartbeats;

    public GsproConnectProxy(
        GsproConfig config,
        GsproMessageParser parser,
        IRawMessageLogger rawLogger,
        IShotEventSink shotSink,
        bool logHeartbeats = false)
    {
        _config = config;
        _parser = parser;
        _rawLogger = rawLogger;
        _shotSink = shotSink;
        _logHeartbeats = logHeartbeats;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Parse(_config.ListenHost), _config.ListenPort);
        listener.Start();
        Console.WriteLine(
            $"[proxy] listening for LM on {_config.ListenHost}:{_config.ListenPort} → " +
            $"GSPro {_config.UpstreamHost}:{_config.UpstreamPort}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lmClient = await listener.AcceptTcpClientAsync(cancellationToken);
                Console.WriteLine("[proxy] launch monitor connected");
                _ = HandleSessionAsync(lmClient, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleSessionAsync(TcpClient lmClient, CancellationToken cancellationToken)
    {
        using var lm = lmClient;
        using var gspro = new TcpClient();

        try
        {
            await gspro.ConnectAsync(_config.UpstreamHost, _config.UpstreamPort, cancellationToken);
            Console.WriteLine("[proxy] connected to GSPro upstream");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[proxy] failed to connect upstream: {ex.Message}");
            return;
        }

        await using var lmStream = lm.GetStream();
        await using var gsproStream = gspro.GetStream();

        var lmToGspro = PipeAsync(
            lmStream,
            gsproStream,
            TrafficDirection.LaunchMonitorToGspro,
            cancellationToken);

        var gsproToLm = PipeAsync(
            gsproStream,
            lmStream,
            TrafficDirection.GsproToLaunchMonitor,
            cancellationToken);

        await Task.WhenAny(lmToGspro, gsproToLm);
        Console.WriteLine("[proxy] session ended");
    }

    private async Task PipeAsync(
        NetworkStream source,
        NetworkStream destination,
        string direction,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var framer = new NewlineJsonFramer();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                await destination.FlushAsync(cancellationToken);

                foreach (var json in framer.Push(buffer.AsSpan(0, read)))
                    await HandleMessageAsync(direction, json, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        catch (IOException)
        {
            // peer closed
        }
    }

    private async Task HandleMessageAsync(
        string direction,
        string json,
        CancellationToken cancellationToken)
    {
        RawTrafficMessage parsed;
        try
        {
            parsed = _parser.Parse(direction, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[proxy] parse error ({direction}): {ex.Message}");
            Console.WriteLine($"  raw: {json}");
            return;
        }

        if (!_logHeartbeats && parsed.Shot?.IsHeartBeat == true)
            return;

        await _rawLogger.LogAsync(parsed, cancellationToken);
        Console.WriteLine($"[raw {direction}] {Truncate(json, 180)}");

        if (parsed.UnknownFields is { Count: > 0 })
            Console.WriteLine(
                $"  ! UNKNOWN KEYS (spike hit?): {string.Join(", ", parsed.UnknownFields.Keys)}");

        // Fire-and-forget: WledShotEffectSink's Idle/Waiting/NotReady holds run indefinitely
        // until superseded by the next sink call. Awaiting one synchronously here would block
        // this same loop from reading the next LM/GSPro message — including the very message
        // that would supersede/end the hold — permanently stalling the proxy pipe.
        void OnError(string message) => Console.WriteLine($"[proxy] shot sink error: {message}");

        if (parsed.Shot is { } shot)
        {
            if (shot.HasBallData)
                SinkCallDispatcher.Fire(() => _shotSink.OnShotAsync(shot, cancellationToken), OnError);
            else if (shot.IsBallDetected)
                SinkCallDispatcher.Fire(() => _shotSink.OnBallReadyAsync(shot, cancellationToken), OnError);
        }

        if (parsed.Response is { } response)
            SinkCallDispatcher.Fire(() => _shotSink.OnPlayerInfoAsync(response, cancellationToken), OnError);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
