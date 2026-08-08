using GsproLighting.Core.Config;
using GsproLighting.Ui.Hosting;
using GsproLighting.Ui.Updates;
using Velopack;

namespace GsproLighting.Ui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Required by Velopack pack verification and update apply hooks.
        VelopackApp.Build().Run();

        // Generated initialization configures per-monitor DPI awareness before controls exist.
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            CrashLog.Show("GSPro Lighting — UI error", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                CrashLog.Show("GSPro Lighting — fatal error", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        try
        {
            var store = new ConfigStore();
            var coordinator = new LightingAppCoordinator(store);
            var updates = new AppUpdateService();
            Application.Run(new TrayApplicationContext(coordinator, updates));
        }
        catch (Exception ex)
        {
            CrashLog.Show("GSPro Lighting failed to start", ex);
        }
    }
}
