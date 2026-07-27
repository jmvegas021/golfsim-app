using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Best-effort probe for Connect log paths via process working directory / open-file hints.
/// Full handle enumeration needs admin; this stays unprivileged.
/// </summary>
public sealed class ConnectOpenLogProbe
{
    private static readonly Regex PathHint = new(
        @"[A-Za-z]:\\(?:[^""\r\n:*?<>|]+\\)*(?:[^""\r\n:*?<>|]+\.(?:log|txt|out))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<string> Probe(IReadOnlyList<ConnectProcessPath> processes)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in processes)
        {
            AddWorkingDirectoryLogs(found, process.ProcessId);
            if (!string.IsNullOrWhiteSpace(process.Directory))
                AddNearbyUnityLogs(found, process.Directory);
        }

        return found.Where(File.Exists).ToList();
    }

    private static void AddWorkingDirectoryLogs(HashSet<string> found, int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            string? cwd = null;
            try
            {
                // MainModule directory is the reliable fallback; StartInfo.WorkingDirectory is often empty.
                cwd = process.StartInfo.WorkingDirectory;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(cwd) || !Directory.Exists(cwd))
                return;

            foreach (var file in SafeListLogs(cwd))
                found.Add(file);
        }
        catch
        {
            // Process may have exited.
        }
    }

    private static void AddNearbyUnityLogs(HashSet<string> found, string directory)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(directory, "output_log.txt"),
                     Path.Combine(directory, "Player.log"),
                     Path.Combine(directory, "Connect.log"),
                     Path.Combine(directory, "GSPconnect.log")
                 })
        {
            if (File.Exists(candidate))
                found.Add(candidate);
        }

        try
        {
            foreach (var dataDir in Directory.EnumerateDirectories(directory, "*_Data"))
            {
                var outputLog = Path.Combine(dataDir, "output_log.txt");
                if (File.Exists(outputLog))
                    found.Add(outputLog);
            }
        }
        catch
        {
            // Permission / race.
        }
    }

    private static IEnumerable<string> SafeListLogs(string directory)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file);
            if (ext.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("output_log.txt", StringComparison.OrdinalIgnoreCase) ||
                PathHint.IsMatch(file))
                yield return file;
        }
    }
}
