namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Scans known GSPro / Connect AppData locations for active log files.
/// </summary>
public sealed class ConnectLogDiscoverer
{
    private static readonly string[] NameHints =
    {
        "gspro", "connect", "gsp", "garmin", "r50", "output_log", "player.log"
    };

    private static readonly string[] Extensions = { ".log", ".txt", ".out" };

    public IReadOnlyList<DiscoveredLogFile> Discover(DateTimeOffset? newerThanUtc = null)
    {
        var cutoff = newerThanUtc ?? DateTimeOffset.UtcNow.AddHours(-48);
        var roots = EnumerateRoots().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var found = new Dictionary<string, DiscoveredLogFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
            ScanRoot(root, cutoff, found);

        return found.Values
            .OrderByDescending(f => f.LastWriteUtc)
            .Take(12)
            .ToList();
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            yield return local;
            var localLow = Path.GetFullPath(Path.Combine(local, "..", "LocalLow"));
            yield return Path.Combine(localLow, "GSPro");
            yield return localLow;
            yield return Path.Combine(local, "GSPro");
            yield return Path.Combine(local, "GSPro Connect");
            yield return Path.Combine(local, "Garmin");
        }

        if (!string.IsNullOrWhiteSpace(roaming))
        {
            yield return Path.Combine(roaming, "GSPro");
            yield return Path.Combine(roaming, "GSPro Connect");
            yield return Path.Combine(roaming, "Garmin");
            yield return roaming;
        }
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

                FileInfo info;
                try
                {
                    info = new FileInfo(path);
                }
                catch
                {
                    continue;
                }

                if (!info.Exists || info.Length == 0)
                    continue;

                var write = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                if (write < cutoff && info.Length < 2_000_000)
                    continue;

                found[path] = new DiscoveredLogFile
                {
                    FullPath = path,
                    LastWriteUtc = write,
                    LengthBytes = info.Length
                };
            }
        }
        catch
        {
            // Best-effort scan — permission errors are expected on some trees.
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        var depth = 0;

        while (stack.Count > 0 && depth < 8_000)
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

    private static bool IsNoiseDirectory(string name) =>
        name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Caches", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Packages", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Google", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("npm", StringComparison.OrdinalIgnoreCase);

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
