using System.Text;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Services;
using GsproLighting.Gspro.Dispatch;
using GsproLighting.Gspro.Parsing;

namespace GsproLighting.Gspro.Watchers;

/// <summary>
/// Tails GSPro/Connect log files from EOF and feeds parsed shots / raw lines.
/// </summary>
public sealed class ConnectLogTailWatcher : IAsyncDisposable
{
    private readonly ShotFeedBuffer _feed;
    private readonly IShotEventSink _sink;
    private readonly string _rawLogDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, TailState> _tails = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private string? _lastRawLine;
    private int _interestingCount;
    private string _watchedSignature = string.Empty;

    public ConnectLogTailWatcher(ShotFeedBuffer feed, IShotEventSink sink, string rawLogDirectory)
    {
        _feed = feed;
        _sink = sink;
        _rawLogDirectory = rawLogDirectory;
    }

    public string? LastRawLine => _lastRawLine;
    public int InterestingCount => _interestingCount;
    public IReadOnlyList<string> WatchedPaths
    {
        get
        {
            lock (_gate)
                return _tails.Keys.ToList();
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_loop is { IsCompleted: false })
                return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public void UpdateWatchedFiles(IEnumerable<string> paths)
    {
        List<string> added;
        lock (_gate)
        {
            // Discoverer already ranks ball-metric logs first; keep a wider watch set.
            var desired = paths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in _tails.Keys.ToList())
            {
                if (desired.Contains(existing))
                    continue;
                _tails[existing].Dispose();
                _tails.Remove(existing);
            }

            added = new List<string>();
            foreach (var path in desired)
            {
                if (_tails.ContainsKey(path))
                    continue;
                _tails[path] = TailState.OpenAtEnd(path);
                added.Add(path);
            }

            var signature = string.Join("|", desired.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
            if (!string.Equals(signature, _watchedSignature, StringComparison.OrdinalIgnoreCase))
            {
                _watchedSignature = signature;
                EmitWatchStatus(desired);
            }
        }

        foreach (var path in added)
            _feed.AddRaw("LOG", $"Tailing {ShortPath(path)}");
    }

    public void NotifyNoLogsFound()
    {
        lock (_gate)
        {
            if (_tails.Count > 0)
                return;
            if (string.Equals(_watchedSignature, "__none__", StringComparison.Ordinal))
                return;
            _watchedSignature = "__none__";
        }

        _feed.AddRaw(
            "LOG",
            "No Connect logs found under AppData — watching network only");
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
            foreach (var tail in _tails.Values)
                tail.Dispose();
            _tails.Clear();
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
    }

    private void EmitWatchStatus(HashSet<string> paths)
    {
        if (paths.Count == 0)
            return;
        var names = string.Join(", ", paths.Take(3).Select(ShortPath));
        var more = paths.Count > 3 ? $" (+{paths.Count - 3} more)" : string.Empty;
        _feed.AddRaw("LOG", $"Watching {paths.Count} Connect log(s): {names}{more}");
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            List<TailState> snapshot;
            lock (_gate)
                snapshot = _tails.Values.ToList();

            foreach (var tail in snapshot)
            {
                try
                {
                    await ReadNewLinesAsync(tail, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Keep watching other files.
                }
            }

            await Task.Delay(400, token).ConfigureAwait(false);
        }
    }

    private async Task ReadNewLinesAsync(TailState tail, CancellationToken token)
    {
        if (tail.Stream is null)
            return;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            var line = await ReadLineAsync(tail, token).ConfigureAwait(false);
            if (line is null)
                break;
            if (line.Length == 0)
                continue;
            HandleLine(tail, line, token);
        }
    }

    /// <summary>
    /// Parses a line and dispatches the matching sink call. The dispatch is fire-and-forget
    /// (via SinkCallDispatcher) — WledShotEffectSink's Idle/Waiting/NotReady holds run
    /// indefinitely until superseded by the next call, and synchronously awaiting one here
    /// would block this read loop from ever reaching the next line that's needed to end it.
    /// </summary>
    private void HandleLine(TailState tail, string line, CancellationToken token)
    {
        ConnectParseResult parsed;
        try
        {
            // Per-file parser so sparse HLA / Ready edges from one Connect log
            // cannot be cleared by Force-only lines from another tailed file.
            parsed = tail.Parser.Parse(line);
        }
        catch (Exception ex)
        {
            _feed.AddRaw("LOG", $"Parse error: {ex.Message}");
            return;
        }

        if (parsed.Kind == ConnectParseKind.Ignore)
            return;

        _lastRawLine = line;
        Interlocked.Increment(ref _interestingCount);
        AppendCapture("log", line);

        void OnError(string message) => _feed.AddRaw("LOG", $"Shot emit error: {message}");

        switch (parsed.Kind)
        {
            case ConnectParseKind.Shot when parsed.Shot is not null:
                SinkCallDispatcher.Fire(() => _sink.OnShotAsync(parsed.Shot, token), OnError);
                break;
            case ConnectParseKind.Ready when parsed.Shot is not null:
                SinkCallDispatcher.Fire(() => _sink.OnBallReadyAsync(parsed.Shot, token), OnError);
                break;
            case ConnectParseKind.NotReady:
                SinkCallDispatcher.Fire(() => _sink.OnBallNotReadyAsync(token), OnError);
                break;
            case ConnectParseKind.Waiting:
                SinkCallDispatcher.Fire(() => _sink.OnWaitingAsync(token), OnError);
                break;
            default:
                _feed.AddRaw("LOG", parsed.RawLine ?? line);
                break;
        }
    }

    private void AppendCapture(string source, string line)
    {
        try
        {
            Directory.CreateDirectory(_rawLogDirectory);
            var path = Path.Combine(_rawLogDirectory, $"r50-{source}-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            var payload = $"{{\"ts\":\"{DateTimeOffset.UtcNow:O}\",\"src\":\"{source}\",\"line\":{JsonEscape(line)}}}\n";
            lock (_gate)
                File.AppendAllText(path, payload);
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static string ShortPath(string path)
    {
        try
        {
            var file = Path.GetFileName(path);
            var dir = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            return string.IsNullOrWhiteSpace(dir) ? file : $"{dir}/{file}";
        }
        catch
        {
            return path;
        }
    }

    private static string JsonEscape(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static Task<string?> ReadLineAsync(TailState tail, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var next = tail.Stream!.ReadByte();
            if (next < 0)
            {
                try
                {
                    var info = new FileInfo(tail.Path);
                    if (info.Exists && info.Length < tail.Stream.Position)
                    {
                        tail.ReopenAtStart();
                        continue;
                    }
                }
                catch
                {
                }

                return Task.FromResult<string?>(null);
            }

            if (next is '\n')
            {
                var text = tail.Builder.ToString();
                tail.Builder.Clear();
                if (text.EndsWith('\r'))
                    text = text[..^1];
                return Task.FromResult<string?>(text);
            }

            tail.Builder.Append((char)next);
            if (tail.Builder.Length > 16_384)
            {
                tail.Builder.Clear();
                return Task.FromResult<string?>(null);
            }
        }
    }

    private sealed class TailState : IDisposable
    {
        public required string Path { get; init; }
        public ConnectLogLineParser Parser { get; } = new();
        public FileStream? Stream { get; private set; }
        public StringBuilder Builder { get; } = new();

        public static TailState OpenAtEnd(string path)
        {
            var state = new TailState { Path = path };
            state.Open(seekEnd: true);
            return state;
        }

        public void ReopenAtStart()
        {
            Stream?.Dispose();
            Open(seekEnd: false);
        }

        private void Open(bool seekEnd)
        {
            Stream = new FileStream(
                Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (seekEnd)
                Stream.Seek(0, SeekOrigin.End);
        }

        public void Dispose() => Stream?.Dispose();
    }
}
