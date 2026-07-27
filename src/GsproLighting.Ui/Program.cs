using GsproLighting.Core.Config;
using GsproLighting.Ui.Hosting;

namespace GsproLighting.Ui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
            CrashLog.Show("GSPro Lighting — UI error", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                CrashLog.Show("GSPro Lighting — fatal error", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        try
        {
            var store = new ConfigStore();
            var coordinator = new LightingAppCoordinator(store);
            Application.Run(new TrayApplicationContext(coordinator));
        }
        catch (Exception ex)
        {
            CrashLog.Show("GSPro Lighting failed to start", ex);
        }
    }
}
