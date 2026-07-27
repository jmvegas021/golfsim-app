using GsproLighting.Core.Config;

namespace GsproLighting.Ui;

internal static class CrashLog
{
    public static void Write(string source, Exception exception)
    {
        try
        {
            var line =
                $"[{DateTime.Now:O}] {source}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(AppPaths.CrashLogPath, line);
        }
        catch
        {
            // last resort — avoid throwing from the logger itself
        }
    }

    public static void Show(string title, Exception exception)
    {
        Write(title, exception);
        try
        {
            MessageBox.Show(
                $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Details were written to:{Environment.NewLine}{AppPaths.CrashLogPath}",
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
