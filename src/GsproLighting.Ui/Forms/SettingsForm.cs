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
    private readonly EffectsTabPanel _effects;
    private readonly QuickControlTabPanel _quickControl;
    private readonly PreviewTabPanel _preview;
    private readonly WledTabPanel _wled;
    private readonly ConnectionTabPanel _connection = new();
    private readonly LiveFeedTabPanel _liveFeed;
    private readonly UpdatesPanel _updatesPanel;
    private readonly NightTabControl _tabs = new();
    private readonly ChromeFooter _footer = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 2000 };
    private readonly SettingsFormActions _actions;

    public SettingsForm(LightingAppCoordinator app, AppUpdateService updates)
    {
        _app = app;
        Text = "GSPro Lighting — Settings";
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(820, 640);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();
        FormBorderStyle = FormBorderStyle.Sizable;
        Icon = AppIconLoader.AppIcon;
        KeyPreview = true;
        // Defer sizing to OnLoad: the handle (and its resolved PerMonitorV2 DPI) doesn't exist
        // yet here, so Bounds computed now from Screen.WorkingArea (already real screen pixels)
        // would get auto-scaled a second time by AutoScaleMode.Dpi once the handle is created —
        // the window ends up DPI-factor-times too large and runs off the right edge of the
        // screen (title bar controls included) on any display above 100% scaling.
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(0, 0, 1080, 800);

        _effects = new EffectsTabPanel();
        _quickControl = new QuickControlTabPanel(
            ResolveEffectsForPreview,
            () => _app.Config.Wled,
            ResolveControllerIp,
            app.Preview);
        _preview = new PreviewTabPanel(ResolveEffectsForPreview, () => _app.Config.Wled, app.Preview);
        _wled = new WledTabPanel(ResolveControllerIp, () => _connection.Brightness);
        _updatesPanel = new UpdatesPanel(updates);
        _liveFeed = new LiveFeedTabPanel(
            app.Feed,
            new LogsFolderLauncher(app.Config.Logging.RawLogDirectory),
            new LogExportService(app.Config.Logging.RawLogDirectory, AppPaths.CrashLogPath),
            () => _app.IsConnectSourceActive);
        _actions = new SettingsFormActions(app, _effects, _connection, _liveFeed);

        // Thin theme apply for workstream A/B panels without rewriting internals.
        UiTheme.ApplyTabChrome(_effects);
        UiTheme.ApplyTabChrome(_quickControl);
        UiTheme.ApplyTabChrome(_preview);
        UiTheme.ApplyTabChrome(_wled);

        BuildLayout();
        LoadFromConfig();
        WireEvents();
        _actions.UpdateStatus();
        _statusTimer.Start();
        Shown += (_, _) =>
        {
            PerformLayout();
            _effects.PerformLayout();
            _quickControl.PerformLayout();
            _preview.PerformLayout();
            UpdateFooterTip();
            TriggerFirstRunOnboarding();
        };
    }

    /// <summary>
    /// Guides a never-configured install straight to the Connection tab and starts a network
    /// scan for WLED devices, instead of leaving the user to discover Connection → IP field on
    /// their own. Runs every time Settings opens while the IP is still the untouched placeholder
    /// — harmless (a few-second scan) and stops once a real IP is saved.
    /// </summary>
    private void TriggerFirstRunOnboarding()
    {
        if (!_connection.LooksLikeFirstRun)
            return;

        SelectTab("Connection");
        _ = _connection.TriggerScanAsync();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToScreen();
    }

    /// <summary>
    /// Clamps/centers the window against the actual working area of the monitor it landed on.
    /// Must run after the handle exists (post-DPI-scale-pass) — see the constructor comment.
    /// </summary>
    private void FitToScreen()
    {
        var workingArea = Screen.FromHandle(Handle).WorkingArea;
        var width = Math.Min(Width, workingArea.Width);
        var height = Math.Min(Height, workingArea.Height);
        var left = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
        var top = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);
        Bounds = new Rectangle(left, top, width, height);
    }

    public async Task FocusUpdatesAndCheckAsync()
    {
        BringToFront();
        Activate();
        SelectTab("Updates");
        _updatesPanel.Focus();
        await _updatesPanel.CheckAsync();
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Oemcomma) || keyData == (Keys.F1))
        {
            ShowAbout();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void BuildLayout()
    {
        Controls.Add(_tabs);
        Controls.Add(_footer);
        Controls.Add(new BrandHeader());

        // Sellable tab order: Effects → Quick control → Preview → WLED → Connection → Live feed → Updates
        AddTab("Effects", _effects);
        AddTab("Quick control", _quickControl);
        AddTab("Preview", _preview);
        AddTab("WLED", _wled);
        AddTab("Connection", _connection);
        AddTab("Live feed", _liveFeed);
        AddTab("Updates", BuildUpdatesWrapper());

        _footer.AboutRequested += (_, _) => ShowAbout();
        _tabs.SelectedIndexChanged += (_, _) => UpdateFooterTip();
    }

    private void AddTab(string title, Control content)
    {
        var page = new TabPage(title)
        {
            BackColor = UiTheme.Background,
            ForeColor = UiTheme.Text,
            Padding = new Padding(0),
            UseVisualStyleBackColor = false
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
            Padding = new Padding(24)
        };
        wrapper.Paint += (_, e) => UiTheme.FillNightBackground(e.Graphics, wrapper.ClientRectangle);
        _updatesPanel.Dock = DockStyle.Top;
        wrapper.Controls.Add(_updatesPanel);
        return wrapper;
    }

    private void ExportConfig()
    {
        // Export exactly what's on screen, not just the last-saved state.
        if (!_actions.Save())
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Export GSPro Lighting settings",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "gspro-lighting-settings.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            new ConfigStore(dialog.FileName).Save(_app.Config);
            _connection.SetBackupStatus($"Exported to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _connection.SetBackupStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    private void ImportConfig()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import GSPro Lighting settings",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var imported = new ConfigStore(dialog.FileName).Load();
            _app.SaveConfig(imported);
            LoadFromConfig();
            _connection.SetBackupStatus($"Imported from {dialog.FileName} — settings applied.");
        }
        catch (Exception ex)
        {
            _connection.SetBackupStatus($"Import failed: {ex.Message}", isError: true);
        }
    }

    private void LoadFromConfig()
    {
        _effects.LoadConfig(_app.Config.Effects);
        _connection.LoadConfig(_app.Config);
        _liveFeed.ExportIncludeDays = _app.Config.Logging.ExportIncludeDays;
        _preview.RefreshFromEffects();
        _quickControl.RefreshFromEffects();
    }

    private void WireEvents()
    {
        _effects.SaveRequested += (_, _) =>
        {
            if (_actions.Save())
            {
                _preview.RefreshFromEffects();
                _quickControl.RefreshFromEffects();
            }
        };
        _effects.TestRequested += async (_, _) => await _actions.TestSweepAsync();
        _effects.IdleRequested += async (_, _) => await _actions.TestIdleAsync();
        _effects.ProxyToggleRequested += async (_, _) => await _actions.ToggleProxyAsync();
        _effects.PreviewRequested += async (_, args) => await _actions.PreviewEffectAsync(args.Slot);
        _connection.ExportRequested += (_, _) => ExportConfig();
        _connection.ImportRequested += (_, _) => ImportConfig();
        _statusTimer.Tick += (_, _) =>
        {
            _actions.UpdateStatus();
            _liveFeed.RefreshEmptyState();
        };
        _app.ProxyStateChanged += OnAppStatusChanged;
        _app.R50StatusChanged += OnAppStatusChanged;
        FormClosed += (_, _) => UnwireEvents();
    }

    private EffectConfig ResolveEffectsForPreview()
    {
        // In-memory overlay of Effects-tab edits without persisting (no Save).
        var working = _app.Config.Effects.Clone();
        _effects.ApplyTo(working);
        return working;
    }

    private string ResolveControllerIp() =>
        string.IsNullOrWhiteSpace(_connection.ControllerIp)
            ? _app.Config.Wled.ControllerIp
            : _connection.ControllerIp;

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
            BeginInvoke(() =>
            {
                _actions.UpdateStatus();
                _liveFeed.RefreshEmptyState();
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SelectTab(string title)
    {
        foreach (TabPage page in _tabs.TabPages)
        {
            if (!string.Equals(page.Text, title, StringComparison.Ordinal))
                continue;
            _tabs.SelectedTab = page;
            return;
        }
    }

    private void UpdateFooterTip()
    {
        var tip = _tabs.SelectedTab?.Text switch
        {
            "Effects" => "Save writes config. Test lights / Idle glow preview without leaving this tab.",
            "Preview" => ProductCopy.PreviewHint,
            "WLED" => ProductCopy.WledTabTip,
            "Connection" => ProductCopy.NoWledBody,
            "Live feed" => ProductCopy.LiveFeedWaitingBody,
            "Updates" => ProductCopy.UpdatesIntro,
            _ => ProductCopy.BrandSubtitle
        };
        _footer.SetTip(tip);
    }

    private void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog(this);
    }
}
