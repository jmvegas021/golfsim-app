using System.IO.Compression;
using GsproLighting.Core.Config;

namespace GsproLighting.Core.Logging;

public sealed class LogExportService
{
    private readonly LogExportFileSelector _fileSelector;

    public LogExportService()
        : this(AppPaths.LogsDirectory, AppPaths.CrashLogPath, TimeProvider.System)
    {
    }

    public LogExportService(
        string logsDirectory,
        string crashLogPath,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(crashLogPath);

        _fileSelector = new LogExportFileSelector(
            Path.GetFullPath(logsDirectory),
            Path.GetFullPath(crashLogPath),
            timeProvider ?? TimeProvider.System);
    }

    public LogExportResult Export(string destinationPath, int includeDays = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(includeDays, 1);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        EnsureDestinationDirectoryExists(fullDestinationPath);

        var sourcePaths = _fileSelector.Select(includeDays, fullDestinationPath);
        if (sourcePaths.Count == 0)
        {
            throw new InvalidOperationException(
                $"No recent GSPro, R50, or crash logs were found for the last {includeDays} day(s).");
        }

        var temporaryPath = $"{fullDestinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            CreateArchive(temporaryPath, sourcePaths);
            File.Move(temporaryPath, fullDestinationPath, overwrite: true);
        }
        catch
        {
            TryDeleteTemporaryArchive(temporaryPath);
            throw;
        }

        return new LogExportResult(
            fullDestinationPath,
            sourcePaths
                .Select(path => Path.GetFileName(path) ?? path)
                .ToArray());
    }

    private static void EnsureDestinationDirectoryExists(string destinationPath)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory) ||
            !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The export destination folder does not exist: {destinationDirectory}");
        }
    }

    private static void CreateArchive(
        string temporaryPath,
        IReadOnlyList<string> sourcePaths)
    {
        using var archiveStream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var archive = new ZipArchive(
            archiveStream,
            ZipArchiveMode.Create,
            leaveOpen: false);

        foreach (var sourcePath in sourcePaths)
        {
            archive.CreateEntryFromFile(
                sourcePath,
                Path.GetFileName(sourcePath),
                CompressionLevel.Optimal);
        }
    }

    private static void TryDeleteTemporaryArchive(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch
        {
            // Preserve the original export failure.
        }
    }
}
