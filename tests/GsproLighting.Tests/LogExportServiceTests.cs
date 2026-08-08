using System.IO.Compression;
using GsproLighting.Core.Logging;
using Xunit;

namespace GsproLighting.Tests;

public sealed class LogExportServiceTests
{
    [Fact]
    public void Export_SelectsDateRangeAndCrashLog_AndCreatesZip()
    {
        using var directory = new TestDirectory();
        var logsDirectory = Directory.CreateDirectory(directory.GetPath("logs")).FullName;
        var crashLogPath = directory.Write("crash.log", "crash");
        WriteLog(logsDirectory, "gspro-raw-20260807.jsonl");
        WriteLog(logsDirectory, "r50-log-20260806.jsonl");
        WriteLog(logsDirectory, "gspro-raw-20260805.jsonl");
        WriteLog(logsDirectory, "unrelated-20260807.jsonl");
        var destinationPath = directory.GetPath("logs.zip");
        var service = CreateService(logsDirectory, crashLogPath);

        var result = service.Export(destinationPath, includeDays: 2);

        Assert.Equal(Path.GetFullPath(destinationPath), result.DestinationPath);
        Assert.Equal(
            ["crash.log", "gspro-raw-20260807.jsonl", "r50-log-20260806.jsonl"],
            result.ExportedFileNames);
        using var archive = ZipFile.OpenRead(destinationPath);
        Assert.Equal(result.ExportedFileNames, archive.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void Export_ExcludesDestinationWhenItMatchesLogPattern()
    {
        using var directory = new TestDirectory();
        var logsDirectory = Directory.CreateDirectory(directory.GetPath("logs")).FullName;
        var crashLogPath = directory.Write("crash.log", "crash");
        var destinationPath = Path.Combine(logsDirectory, "gspro-raw-20260807.jsonl");
        File.WriteAllText(destinationPath, "previous export");
        var service = CreateService(logsDirectory, crashLogPath);

        var result = service.Export(destinationPath);

        Assert.Equal(["crash.log"], result.ExportedFileNames);
        using var archive = ZipFile.OpenRead(destinationPath);
        Assert.Equal("crash.log", Assert.Single(archive.Entries).Name);
    }

    private static LogExportService CreateService(
        string logsDirectory,
        string crashLogPath) =>
        new(
            logsDirectory,
            crashLogPath,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero)));

    private static void WriteLog(string logsDirectory, string fileName) =>
        File.WriteAllText(Path.Combine(logsDirectory, fileName), fileName);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
