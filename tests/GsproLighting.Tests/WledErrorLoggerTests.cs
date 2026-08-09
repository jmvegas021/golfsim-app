using GsproLighting.Core.Logging;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledErrorLoggerTests
{
    [Fact]
    public void Log_WritesJsonlLineWithSourceAndMessage()
    {
        using var directory = new TestDirectory();
        var logsDirectory = Directory.CreateDirectory(directory.GetPath("logs")).FullName;
        var logger = new WledErrorLogger(logsDirectory);

        logger.Log("wled-tab", "WLED returned 400 (Bad Request) — {\"error\":9}");

        var path = Assert.Single(Directory.GetFiles(logsDirectory, "wled-errors-*.jsonl"));
        var line = File.ReadAllText(path).TrimEnd();
        Assert.Contains("\"source\":\"wled-tab\"", line, StringComparison.Ordinal);
        Assert.Contains("WLED returned 400", line, StringComparison.Ordinal);
        Assert.Contains("{\\\"error\\\":9}", line, StringComparison.Ordinal);
        Assert.Contains("\"ts\":", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_EscapesQuotesAndNewlines()
    {
        using var directory = new TestDirectory();
        var logsDirectory = Directory.CreateDirectory(directory.GetPath("logs")).FullName;
        var logger = new WledErrorLogger(logsDirectory);

        logger.Log("quick-control", "line1\n\"quoted\"");

        var line = File.ReadAllText(Directory.GetFiles(logsDirectory, "wled-errors-*.jsonl")[0]).TrimEnd();
        Assert.Contains("\\n", line, StringComparison.Ordinal);
        Assert.Contains("\\\"quoted\\\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\"quoted\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_AppendsMultipleEntries()
    {
        using var directory = new TestDirectory();
        var logsDirectory = Directory.CreateDirectory(directory.GetPath("logs")).FullName;
        var logger = new WledErrorLogger(logsDirectory);

        logger.Log("effect-sink", "first");
        logger.Log("preview", "second");

        var lines = File.ReadAllLines(Directory.GetFiles(logsDirectory, "wled-errors-*.jsonl")[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("first", lines[0], StringComparison.Ordinal);
        Assert.Contains("second", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsBlankDirectory()
    {
        Assert.ThrowsAny<ArgumentException>(() => new WledErrorLogger(" "));
    }
}
