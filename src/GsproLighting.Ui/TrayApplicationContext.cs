using GsproLighting.Ui.Forms;
using GsproLighting.Ui.Hosting;

namespace GsproLighting.Ui;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly LightingAppCoordinator _app;
    private readonly NotifyIcon _tray;
    private SettingsForm? _settings;

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

        if (!app.Config.Ui.StartMinimizedToTray)
            ShowSettings();

        if (app.Config.Ui.StartProxyOnLaunch)
            app.StartProxy();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Test lights", null, async (_, _) =>
        {
            try
            {
                await _app.Preview.PlaySweepAsync(
                    _app.Config.Effects.PureStrike,
                    _app.Config.Wled.LedCount);
            }
            catch (Exception ex)
            {
                _tray.ShowBalloonTip(3000, "GSPro Lighting", ex.Message, ToolTipIcon.Error);
            }
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Start proxy", null, (_, _) => _app.StartProxy());
        menu.Items.Add("Stop proxy", null, async (_, _) => await _app.StopProxyAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());
        return menu;
    }

    private void ShowSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Show();
            _settings.BringToFront();
            return;
        }

        _settings = new SettingsForm(_app);
        _settings.FormClosed += (_, _) => _settings = null;
        _settings.Show();
    }

    private async Task ExitAsync()
    {
        _tray.Visible = false;
        await _app.DisposeAsync();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _settings?.Dispose();
        }

        base.Dispose(disposing);
    }
}
