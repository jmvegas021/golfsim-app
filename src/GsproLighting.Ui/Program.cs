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
        // Required by Velopack pack verification and update apply hooks. Must run before the
        // single-instance check below — Velopack transiently re-launches this same exe with
        // install/update/uninstall flags, which isn't a real duplicate launch and must be
        // allowed through even while the "real" instance is running.
        VelopackApp.Build().Run();

        // ApplicationConfiguration.Initialize applies ApplicationHighDpiMode (PerMonitorV2)
        // from the project file before any controls exist. Do not call SetHighDpiMode after Initialize.
        ApplicationConfiguration.Initialize();

        // One instance only. Without this, autostart + a manual launch (or a second manual
        // launch) silently spins up two processes that both try to bind the same Open Connect
        // proxy port and both send commands to the same WLED device — the proxy fails with
        // "Only one usage of each socket address..." and WLED updates race/contend, which can
        // look like "nothing reaches the lights" even though ball data is flowing fine.
        //
        // Use WaitOne with a grace period rather than failing immediately on contention:
        // Velopack's own "Install & restart" flow launches the new version while the previous
        // one is still shutting down for a moment, and that legitimate handoff must not be
        // mistaken for a real duplicate launch.
        using var singleInstance = new Mutex(initiallyOwned: false, "Local\\GsproLighting-SingleInstance");
        bool acquiredInstance;
        try
        {
            acquiredInstance = singleInstance.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            // Previous instance crashed without releasing — we now own it; treat as acquired.
            acquiredInstance = true;
        }

        if (!acquiredInstance)
        {
            MessageBox.Show(
                "GSPro Lighting is already running — check your system tray icon.",
                "GSPro Lighting",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

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
