using System.Net.Sockets;
using System.Text;

namespace GsproLighting.Gspro.Simulation;

/// <summary>
/// Sends fixture JSON files to a proxy/GSPro listen port as a fake launch monitor.
/// </summary>
public sealed class FixtureShotInjector
{
    private readonly string _host;
    private readonly int _port;

    public FixtureShotInjector(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task InjectDirectoryAsync(
        string directory,
        TimeSpan delayBetweenShots,
        CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(directory, "*.json").OrderBy(f => f).ToArray();
        if (files.Length == 0)
            throw new InvalidOperationException($"No JSON fixtures in {directory}");

        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, cancellationToken);
        await using var stream = client.GetStream();
        Console.WriteLine($"[injector] connected to {_host}:{_port}, sending {files.Length} fixtures");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(json.Trim() + "\n");
            try
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    $"Failed sending {Path.GetFileName(file)} — is the proxy up and connected to upstream?",
                    ex);
            }

            Console.WriteLine($"[injector] sent {Path.GetFileName(file)}");

            // Drain any immediate responses so the pipe doesn't stall.
            if (client.Available > 0)
            {
                var buffer = new byte[client.Available];
                _ = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            }

            if (delayBetweenShots > TimeSpan.Zero)
                await Task.Delay(delayBetweenShots, cancellationToken);
        }

        // Keep the socket briefly open for upstream replies to flush through the proxy.
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
    }
}
