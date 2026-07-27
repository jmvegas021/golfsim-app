using GsproLighting.Ui.Forms;
using GsproLighting.Ui.Hosting;

namespace GsproLighting.Ui;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly LightingAppCoordinator _app;
    private readonly NotifyIcon _tray;
    private SettingsForm? _settings;
    private bool _exitRequested;

    public TrayApplicationContext(LightingAppCoordinator app)
    {
        _app = app;
        _tray = new NotifyIcon
        {
            Text = "GSPro Lighting",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        // Defer work until the WinForms message loop is running.
        var startupTimer = new System.Windows.Forms.Timer { Interval = 250 };
        startupTimer.Tick += (_, _) =>
        {
            startupTimer.Stop();
            startupTimer.Dispose();
            OnStartup();
        };
        startupTimer.Start();
    }

    private void OnStartup()
    {
        try
        {
            if (!_app.Config.Ui.StartMinimizedToTray)
                ShowSettings();
            else
                _tray.ShowBalloonTip(
                    4000,
                    "GSPro Lighting",
                    "Running in the tray. Double-click the tray icon to open settings.",
                    ToolTipIcon.Info);

            if (!_app.Config.Ui.StartMinimizedToTray)
            {
                _tray.ShowBalloonTip(
                    4000,
                    "GSPro Lighting",
                    "Running — close the window to keep it in the tray.",
                    ToolTipIcon.Info);
            }

            _app.FirstShotObserved += OnFirstShotObserved;

            if (_app.Config.R50Watch.AutoWatchEnabled)
                _app.StartR50AutoWatch();

            if (_app.Config.Ui.StartProxyOnLaunch)
                _app.StartProxy();
        }
        catch (Exception ex)
        {
            CrashLog.Show("GSPro Lighting startup error", ex);
        }
    }

    private void OnFirstShotObserved()
    {
        try
        {
            _tray.ShowBalloonTip(
                4000,
                "GSPro Lighting",
                "R50 / Connect activity detected — live feed updating.",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            CrashLog.Write("FirstShotBalloon", ex);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Test lights", null, (_, _) => _ = TestLightsAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Start R50 auto-watch", null, (_, _) =>
        {
            try
            {
                _app.StartR50AutoWatch();
            }
            catch (Exception ex)
            {
                CrashLog.Show("R50 watch start failed", ex);
            }
        });
        menu.Items.Add("Start Open Connect proxy", null, (_, _) =>
        {
            try
            {
                _app.StartProxy();
            }
            catch (Exception ex)
            {
                CrashLog.Show("Proxy start failed", ex);
            }
        });
        menu.Items.Add("Stop proxy", null, (_, _) => _ = StopProxySafeAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _ = ExitAsync());
        return menu;
    }

    private async Task TestLightsAsync()
    {
        try
        {
            await _app.Preview.PlaySweepAsync(
                _app.Config.Effects.PureStrike,
                _app.Config.Wled.LedCount);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(4000, "GSPro Lighting", ex.Message, ToolTipIcon.Error);
            CrashLog.Write("TestLights", ex);
        }
    }

    private async Task StopProxySafeAsync()
    {
        try
        {
            await _app.StopProxyAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("StopProxy", ex);
        }
    }

    private void ShowSettings()
    {
        if (_exitRequested)
            return;

        if (_settings is { IsDisposed: false })
        {
            if (_settings.WindowState == FormWindowState.Minimized)
                _settings.WindowState = FormWindowState.Normal;
            _settings.Show();
            _settings.BringToFront();
            _settings.Activate();
            return;
        }

        _settings = new SettingsForm(_app);
        _settings.FormClosing += (_, args) =>
        {
            // Hide instead of close so closing the window doesn't look like the app died.
            if (_exitRequested)
                return;

            args.Cancel = true;
            _settings.Hide();
            _tray.ShowBalloonTip(
                2500,
                "GSPro Lighting",
                "Still running in the tray. Right-click the tray icon for menu.",
                ToolTipIcon.Info);
        };
        _settings.Show();
    }

    private async Task ExitAsync()
    {
        _exitRequested = true;
        _tray.Visible = false;
        try
        {
            await _app.DisposeAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Exit", ex);
        }

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _settings?.Dispose();
        }

        base.Dispose(disposing);
    }
}
