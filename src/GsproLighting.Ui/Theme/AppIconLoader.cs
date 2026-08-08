using System.Reflection;

namespace GsproLighting.Ui.Theme;

/// <summary>Loads embedded app / tray icons with SystemIcons fallback.</summary>
public static class AppIconLoader
{
    private static Icon? _appIcon;
    private static Icon? _trayIcon;

    public static Icon AppIcon => _appIcon ??= LoadIcon("GsproLighting.Ui.Assets.app.ico")
                                              ?? CloneSystem(SystemIcons.Application);

    public static Icon TrayIcon => _trayIcon ??= LoadTrayPng()
                                               ?? LoadIcon("GsproLighting.Ui.Assets.app.ico")
                                               ?? CloneSystem(SystemIcons.Application);

    private static Icon? LoadTrayPng()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tray-32.png");
        if (!File.Exists(path))
            return null;

        try
        {
            using var bitmap = new Bitmap(path);
            var handle = bitmap.GetHicon();
            using var fromHandle = Icon.FromHandle(handle);
            // Clone so the Icon owns its handle after FromHandle is disposed.
            return (Icon)fromHandle.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static Icon? LoadIcon(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        // Clone so the Icon owns its own handle after the stream closes.
        using var temp = new Icon(stream);
        return (Icon)temp.Clone();
    }

    private static Icon CloneSystem(Icon source) => (Icon)source.Clone();
}
