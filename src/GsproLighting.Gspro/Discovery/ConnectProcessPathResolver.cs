using System.Diagnostics;

namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Resolves GSPro/Connect process install dirs so log discovery can search beside the exe.
/// </summary>
public sealed class ConnectProcessPathResolver
{
    private static readonly string[] ProcessHints =
    {
        "gspro", "connect", "gsp", "garmin", "r50"
    };

    public IReadOnlyList<ConnectProcessPath> Resolve()
    {
        var results = new List<ConnectProcessPath>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!ProcessHints.Any(h =>
                        process.ProcessName.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string? exePath = null;
                try
                {
                    exePath = process.MainModule?.FileName;
                }
                catch
                {
                    // 32/64-bit or access denied — still keep the process name.
                }

                results.Add(new ConnectProcessPath
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    ExecutablePath = exePath,
                    Directory = string.IsNullOrWhiteSpace(exePath)
                        ? null
                        : Path.GetDirectoryName(exePath)
                });
            }
            catch
            {
                // Process may exit mid-enumeration.
            }
            finally
            {
                try { process.Dispose(); }
                catch { /* ignore */ }
            }
        }

        return results
            .GroupBy(p => p.ProcessId)
            .Select(g => g.First())
            .ToList();
    }
}

public sealed class ConnectProcessPath
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? Directory { get; init; }
}
