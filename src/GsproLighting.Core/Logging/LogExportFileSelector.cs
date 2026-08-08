using System.Globalization;

namespace GsproLighting.Core.Logging;

internal sealed class LogExportFileSelector
{
    private static readonly string[] LogPatterns =
    [
        "gspro-raw-*.jsonl",
        "r50-*.jsonl"
    ];

    private readonly string _logsDirectory;
    private readonly string _crashLogPath;
    private readonly TimeProvider _timeProvider;

    public LogExportFileSelector(
        string logsDirectory,
        string crashLogPath,
        TimeProvider timeProvider)
    {
        _logsDirectory = logsDirectory;
        _crashLogPath = crashLogPath;
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<string> Select(int includeDays, string destinationPath)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);
        var firstIncludedDate = today.AddDays(-(includeDays - 1));
        var selectedPaths = SelectDatedLogs(firstIncludedDate, today)
            .Where(path => !PathsEqual(path, destinationPath))
            .ToList();

        if (File.Exists(_crashLogPath) && !PathsEqual(_crashLogPath, destinationPath))
            selectedPaths.Add(_crashLogPath);

        return selectedPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                path => Path.GetFileName(path) ?? path,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<string> SelectDatedLogs(DateOnly firstDate, DateOnly lastDate)
    {
        if (!Directory.Exists(_logsDirectory))
            yield break;

        foreach (var pattern in LogPatterns)
        {
            foreach (var path in Directory.EnumerateFiles(_logsDirectory, pattern))
            {
                if (TryGetFileDate(path, out var fileDate) &&
                    fileDate >= firstDate &&
                    fileDate <= lastDate)
                {
                    yield return path;
                }
            }
        }
    }

    private static bool TryGetFileDate(string path, out DateOnly fileDate)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var dateText = fileName.Length >= 8 ? fileName[^8..] : string.Empty;

        return DateOnly.TryParseExact(
            dateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out fileDate);
    }

    private static bool PathsEqual(string firstPath, string secondPath) =>
        string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            StringComparison.OrdinalIgnoreCase);
}
