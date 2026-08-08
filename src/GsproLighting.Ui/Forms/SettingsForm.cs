using GsproLighting.Core.Config;
using GsproLighting.Core.Logging;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Hosting;
using GsproLighting.Ui.Logging;
using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Updates;

namespace GsproLighting.Ui.Forms;

public sealed class SettingsForm : Form
{
    private readonly LightingAppCoordinator _app;
    private readonly EffectsTabPanel _effects = new();
    private readonly ConnectionTabPanel _connection = new();
    private readonly LiveFeedTabPanel _liveFeed;
    private readonly UpdatesPanel _updatesPanel;
    private readonly NightTabControl _tabs = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };
    private readonly SettingsFormActions _actions;

    public SettingsForm(LightingAppCoordinator app, AppUpdateService updates)
    {
        _app = app;
        _updatesPanel = new UpdatesPanel(updates);
        _liveFeed = new LiveFeedTabPanel(
            app.Feed,
            new LogsFolderLauncher(app.Config.Logging.RawLogDirectory),
            new LogExportService(app.Config.Logging.RawLogDirectory, AppPaths.CrashLogPath));
        _actions = new SettingsFormActions(app, _effects, _connection, _liveFeed);

        Text = "GSPro Lighting — Settings";
        Width = 1040;
        Height = 780;
        MinimumSize = new Size(820, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();

        BuildLayout();
        LoadFromConfig();
        WireEvents();
        _actions.UpdateStatus();
        _statusTimer.Start();
    }

    public async Task FocusUpdatesAndCheckAsync()
    {
        BringToFront();
        Activate();
        _tabs.SelectedTab = _tabs.TabPages.Cast<TabPage>()
            .First(page => page.Text == "Updates");
        _updatesPanel.Focus();
        await _updatesPanel.CheckAsync();
    }

    private void BuildLayout()
    {
        Controls.Add(_tabs);
        Controls.Add(new BrandHeader());
        AddTab("Effects", _effects);
        AddTab("Connection", _connection);
        AddTab("Live feed", _liveFeed);
        AddTab("Updates", BuildUpdatesWrapper());
    }

    private void AddTab(string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = UiTheme.Background,
            ForeColor = UiTheme.Text,
            Padding = new Padding(0)
        };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
    }

    private Control BuildUpdatesWrapper()
    {
        var wrapper = new Panel
        {
            BackColor = UiTheme.Background,
            Padding = new Padding(28)
        };
        _updatesPanel.Dock = DockStyle.Top;
        wrapper.Controls.Add(_updatesPanel);
        return wrapper;
    }

    private void LoadFromConfig()
    {
        _effects.LoadConfig(_app.Config.Effects);
        _connection.LoadConfig(_app.Config);
        _liveFeed.ExportIncludeDays = _app.Config.Logging.ExportIncludeDays;
    }

    private void WireEvents()
    {
        _effects.SaveRequested += (_, _) => _actions.Save();
        _effects.TestRequested += async (_, _) => await _actions.TestSweepAsync();
        _effects.IdleRequested += async (_, _) => await _actions.TestIdleAsync();
        _effects.ProxyToggleRequested += async (_, _) => await _actions.ToggleProxyAsync();
        _effects.PreviewRequested += async (_, args) => await _actions.PreviewEffectAsync(args.Slot);
        _statusTimer.Tick += (_, _) => _actions.UpdateStatus();
        _app.ProxyStateChanged += OnAppStatusChanged;
        _app.R50StatusChanged += OnAppStatusChanged;
        FormClosed += (_, _) => UnwireEvents();
    }

    private void UnwireEvents()
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        _app.ProxyStateChanged -= OnAppStatusChanged;
        _app.R50StatusChanged -= OnAppStatusChanged;
    }

    private void OnAppStatusChanged()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(_actions.UpdateStatus);
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
