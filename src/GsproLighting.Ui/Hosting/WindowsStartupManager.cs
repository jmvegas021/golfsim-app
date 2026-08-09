using Microsoft.Win32;

namespace GsproLighting.Ui.Hosting;

/// <summary>
/// Registers/unregisters a per-user Windows autostart entry via the Run registry key.
/// Combined with UiConfig.StartMinimizedToTray, this lets the app launch straight into the
/// tray alongside Windows with zero manual steps — see PRD "Zero manual input during play"
/// and the v1.0 "autostart" milestone.
/// </summary>
public static class WindowsStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GsproLighting";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing && !string.IsNullOrWhiteSpace(existing);
    }

    public static void Register()
    {
        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
            return;

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key?.SetValue(ValueName, $"\"{exePath}\"");
    }

    public static void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static void Apply(bool enabled)
    {
        if (enabled)
            Register();
        else
            Unregister();
    }

    private static string? ResolveExecutablePath() => Environment.ProcessPath;
}
