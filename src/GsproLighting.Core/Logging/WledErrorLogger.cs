namespace GsproLighting.Core.Logging;

/// <summary>
/// Persists WLED HTTP/DRGB failures (bad requests, timeouts, unreachable device) to disk so
/// they survive past the in-memory Live Feed display and are included in exported log zips.
/// Previously these were only ever shown transiently on screen — impossible to recover or share
/// for diagnosis once the app moved on or the window scrolled.
/// </summary>
public sealed class WledErrorLogger
{
    private readonly string _logsDirectory;
    private readonly object _gate = new();

    public WledErrorLogger(string logsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        _logsDirectory = Path.GetFullPath(logsDirectory);
    }

    public void Log(string source, string message)
    {
        try
        {
            Directory.CreateDirectory(_logsDirectory);
            var path = Path.Combine(_logsDirectory, $"wled-errors-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            var payload =
                $"{{\"ts\":\"{DateTimeOffset.UtcNow:O}\",\"source\":{JsonEscape(source)}," +
                $"\"message\":{JsonEscape(message)}}}\n";
            lock (_gate)
                File.AppendAllText(path, payload);
        }
        catch
        {
            // Logging must never break the caller.
        }
    }

    private static string JsonEscape(string value) =>
        "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}
