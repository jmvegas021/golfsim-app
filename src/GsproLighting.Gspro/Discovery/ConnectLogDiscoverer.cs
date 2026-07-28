namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Scans AppData, ProgramData, and Connect process install dirs for active log files.
/// </summary>
public sealed class ConnectLogDiscoverer
{
    private static readonly string[] NameHints =
    {
        "gspro", "connect", "gsp", "garmin", "r50", "gspconnect",
        "output_log", "player.log", "player-prev", "launchmonitor"
    };

    private static readonly string[] Extensions = { ".log", ".txt", ".out" };

    private readonly ConnectLogCandidateRoots _roots = new();
    private readonly ConnectOpenLogProbe _openProbe = new();

    public IReadOnlyList<DiscoveredLogFile> Discover(
        IReadOnlyList<ConnectProcessPath>? processes = null,
        DateTimeOffset? newerThanUtc = null)
    {
        var cutoff = newerThanUtc ?? DateTimeOffset.UtcNow.AddHours(-72);
        var found = new Dictionary<string, DiscoveredLogFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in _roots.Build(processes))
            ScanRoot(root, cutoff, found);

        if (processes is { Count: > 0 })
        {
            foreach (var path in _openProbe.Probe(processes))
                TryAdd(found, path, cutoff, requireHint: false);
        }

        return found.Values
            .Select(f => new
            {
                File = f,
                ContentScore = ConnectLogContentRanker.ScoreFile(f.FullPath)
            })
            .OrderByDescending(x => x.ContentScore)
            .ThenByDescending(x => x.File.LastWriteUtc)
            .Take(16)
            .Select(x => x.File)
            .ToList();
    }

    private static void ScanRoot(
        string root,
        DateTimeOffset cutoff,
        Dictionary<string, DiscoveredLogFile> found)
    {
        try
        {
            foreach (var path in SafeEnumerateFiles(root))
            {
                if (!LooksLikeConnectLog(path))
                    continue;
                TryAdd(found, path, cutoff, requireHint: true);
            }
        }
        catch
        {
            // Best-effort scan — permission errors are expected on some trees.
        }
    }

    private static void TryAdd(
        Dictionary<string, DiscoveredLogFile> found,
        string path,
        DateTimeOffset cutoff,
        bool requireHint)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
        }
        catch
        {
            return;
        }

        if (!info.Exists || info.Length == 0)
            return;
        if (requireHint && !LooksLikeConnectLog(path))
            return;

        var write = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
        if (write < cutoff && info.Length < 2_000_000)
            return;

        found[path] = new DiscoveredLogFile
        {
            FullPath = path,
            LastWriteUtc = write,
            LengthBytes = info.Length
        };
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        var depth = 0;
        var maxDepth = IsBroadAppDataRoot(root) ? 4_000 : 8_000;

        while (stack.Count > 0 && depth < maxDepth)
        {
            depth++;
            var dir = stack.Pop();
            IEnumerable<string> files;
            IEnumerable<string> children;
            try
            {
                files = Directory.EnumerateFiles(dir);
                children = Directory.EnumerateDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return file;

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name.StartsWith(".", StringComparison.Ordinal))
                    continue;
                if (IsNoiseDirectory(name))
                    continue;
                stack.Push(child);
            }
        }
    }

    private static bool IsBroadAppDataRoot(string root)
    {
        var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Roaming", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("LocalLow", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("ProgramData", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoiseDirectory(string name) =>
        name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Caches", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Google", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("CrashDumps", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeConnectLog(string path)
    {
        var file = Path.GetFileName(path);
        var ext = Path.GetExtension(file);
        if (!Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)) &&
            !file.Equals("output_log.txt", StringComparison.OrdinalIgnoreCase))
            return false;

        var haystack = path.Replace('\\', '/');
        return NameHints.Any(h => haystack.Contains(h, StringComparison.OrdinalIgnoreCase));
    }
}
