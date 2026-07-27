namespace GsproLighting.Core.Config;

public static class AppPaths
{
    public static string InstallDirectory
    {
        get
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    return dir;
            }

            return AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }
    }

    public static string ConfigFilePath =>
        Path.Combine(InstallDirectory, "config", "appsettings.json");

    public static string LogsDirectory =>
        Path.Combine(InstallDirectory, "logs");

    public static string CrashLogPath =>
        Path.Combine(InstallDirectory, "crash.log");
}
