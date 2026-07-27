using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GsproLighting.Gspro.Simulation;

/// <summary>
/// Minimal GSPro Open Connect stand-in for offline Mac development / replay tests.
/// Accepts LM JSON, replies with 200 (or 201 when club-related options change).
/// </summary>
public sealed class MockGsproServer
{
    private readonly string _host;
    private readonly int _port;
    private string _handed = "RH";
    private string _club = "DR";

    public MockGsproServer(string host = "127.0.0.1", int port = 921)
    {
        _host = host;
        _port = port;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Parse(_host), _port);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Mock GSPro could not bind {_host}:{_port}. " +
                "On macOS/Linux use a port >= 1024 (replay uses 9921).",
                ex);
        }

        Console.WriteLine($"[mock-gspro] listening on {_host}:{_port}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                Console.WriteLine("[mock-gspro] client connected");
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        await using var stream = client.GetStream();
        var buffer = new byte[8192];
        var pending = new StringBuilder();

        // Send initial player info like GSPro often does on connect.
        await WriteJsonAsync(stream, BuildPlayerInfo(), cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                pending.Append(Encoding.UTF8.GetString(buffer, 0, read));
                foreach (var message in ExtractJsonObjects(pending))
                    await RespondAsync(stream, message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }

        Console.WriteLine("[mock-gspro] client disconnected");
    }

    private async Task RespondAsync(NetworkStream stream, string json, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var isHeartbeat = root.TryGetProperty("ShotDataOptions", out var options) &&
                          options.TryGetProperty("IsHeartBeat", out var hb) &&
                          hb.ValueKind == JsonValueKind.True;

        if (isHeartbeat)
            return;

        // Undocumented spike fixtures can include Outcome to validate logger detection.
        if (root.TryGetProperty("SimulateOutcome", out var outcome))
        {
            var payload = new Dictionary<string, object?>
            {
                ["Code"] = 250,
                ["Message"] = "Simulated undocumented outcome (spike fixture)",
                ["Outcome"] = outcome.GetString(),
                ["Player"] = new { Handed = _handed, Club = _club }
            };
            await WriteJsonAsync(stream, JsonSerializer.Serialize(payload), cancellationToken);
            return;
        }

        await WriteJsonAsync(stream, """{"Code":200,"Message":"Shot received successfully"}""", cancellationToken);
    }

    private string BuildPlayerInfo() =>
        JsonSerializer.Serialize(new
        {
            Code = 201,
            Message = "GSPro Player Information",
            Player = new { Handed = _handed, Club = _club }
        });

    private static async Task WriteJsonAsync(
        NetworkStream stream,
        string json,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static IEnumerable<string> ExtractJsonObjects(StringBuilder pending)
    {
        var content = pending.ToString();
        var messages = new List<string>();
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    messages.Add(content[start..(i + 1)]);
                    start = -1;
                }
            }
        }

        if (messages.Count == 0)
            return messages;

        var lastEnd = content.LastIndexOf('}') + 1;
        pending.Clear();
        if (lastEnd < content.Length)
            pending.Append(content[lastEnd..]);

        return messages;
    }
}
