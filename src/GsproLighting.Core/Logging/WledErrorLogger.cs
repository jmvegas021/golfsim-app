namespace GsproLighting.Core.Logging;

/// <summary>
/// Persists WLED HTTP/DDP failures (bad requests, timeouts, unreachable device) to disk so
/// they survive past the in-memory Live Feed display and are included in exported log zips.
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

    public void Log(string source, string message) =>
        LogDetailed(source, message, ip: null, url: null, request: null, response: null, hint: null);

    public void LogDetailed(
        string source,
        string message,
        string? ip,
        string? url,
        string? request,
        string? response,
        string? hint)
    {
        try
        {
            Directory.CreateDirectory(_logsDirectory);
            var path = Path.Combine(_logsDirectory, $"wled-errors-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            var payload =
                $"{{\"ts\":\"{DateTimeOffset.UtcNow:O}\"," +
                $"\"source\":{JsonEscape(source)}," +
                $"\"ip\":{JsonEscape(ip ?? "")}," +
                $"\"url\":{JsonEscape(url ?? "")}," +
                $"\"request\":{JsonEscape(request ?? "")}," +
                $"\"response\":{JsonEscape(response ?? "")}," +
                $"\"hint\":{JsonEscape(hint ?? "")}," +
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
