namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Builds candidate directories where GSPconnect / Garmin / GSPro logs commonly live.
/// </summary>
public sealed class ConnectLogCandidateRoots
{
    private readonly ConnectProcessPathResolver _processPaths = new();

    public IReadOnlyList<string> Build(IReadOnlyList<ConnectProcessPath>? processes = null)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumerateAppDataRoots())
            TryAdd(roots, root);

        TryAdd(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        TryAdd(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GSPro"));
        TryAdd(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Garmin"));

        var resolved = processes ?? _processPaths.Resolve();
        foreach (var process in resolved)
            AddProcessRoots(roots, process);

        return roots.Where(Directory.Exists).ToList();
    }

    private static void AddProcessRoots(HashSet<string> roots, ConnectProcessPath process)
    {
        if (string.IsNullOrWhiteSpace(process.Directory))
            return;

        TryAdd(roots, process.Directory);
        TryAdd(roots, Path.Combine(process.Directory, "Logs"));
        TryAdd(roots, Path.Combine(process.Directory, "Log"));
        TryAdd(roots, Path.Combine(process.Directory, "logs"));

        // Unity-style: GSPconnect_Data\output_log.txt beside the exe.
        foreach (var dataDir in SafeEnumerateDataDirs(process.Directory))
            TryAdd(roots, dataDir);

        var parent = Directory.GetParent(process.Directory)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            TryAdd(roots, parent);
            foreach (var dataDir in SafeEnumerateDataDirs(parent))
                TryAdd(roots, dataDir);
        }
    }

    private static IEnumerable<string> SafeEnumerateDataDirs(string directory)
    {
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(directory, "*_Data");
        }
        catch
        {
            yield break;
        }

        foreach (var child in children)
            yield return child;
    }

    private static IEnumerable<string> EnumerateAppDataRoots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            yield return local;
            var localLow = Path.GetFullPath(Path.Combine(local, "..", "LocalLow"));
            yield return localLow;
            yield return Path.Combine(localLow, "GSPro");
            yield return Path.Combine(localLow, "Garmin");
            yield return Path.Combine(local, "GSPro");
            yield return Path.Combine(local, "GSPro Connect");
            yield return Path.Combine(local, "GSPconnect");
            yield return Path.Combine(local, "Garmin");
        }

        if (!string.IsNullOrWhiteSpace(roaming))
        {
            yield return roaming;
            yield return Path.Combine(roaming, "GSPro");
            yield return Path.Combine(roaming, "GSPro Connect");
            yield return Path.Combine(roaming, "GSPconnect");
            yield return Path.Combine(roaming, "Garmin");
        }
    }

    private static void TryAdd(HashSet<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            roots.Add(Path.GetFullPath(path));
        }
        catch
        {
            // Invalid path — skip.
        }
    }
}
