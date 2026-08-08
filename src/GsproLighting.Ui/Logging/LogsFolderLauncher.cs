using System.Diagnostics;
using GsproLighting.Core.Config;

namespace GsproLighting.Ui.Logging;

public sealed class LogsFolderLauncher
{
    private readonly string _logsDirectory;

    public LogsFolderLauncher()
        : this(AppPaths.LogsDirectory)
    {
    }

    public LogsFolderLauncher(string logsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        _logsDirectory = Path.GetFullPath(logsDirectory);
    }

    public void Open()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Opening the logs folder is supported only on Windows.");

        if (!Directory.Exists(_logsDirectory))
            throw new DirectoryNotFoundException($"The logs folder does not exist: {_logsDirectory}");

        Process.Start(new ProcessStartInfo
        {
            FileName = _logsDirectory,
            UseShellExecute = true
        });
    }
}
