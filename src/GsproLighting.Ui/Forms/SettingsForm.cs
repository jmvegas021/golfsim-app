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
    private readonly PreviewTabPanel _preview;
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
        MinimumSize = new Size(860, 720);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();
        FormBorderStyle = FormBorderStyle.Sizable;
        Icon = AppIconLoader.AppIcon;
        KeyPreview = true;
        ApplyInitialBounds();

        _effects = new EffectsTabPanel();
        _preview = new PreviewTabPanel(ResolveEffectsForPreview, () => _app.Config.Wled, app.Preview);
        _updatesPanel = new UpdatesPanel(updates);
        _liveFeed = new LiveFeedTabPanel(
            app.Feed,
            new LogsFolderLauncher(app.Config.Logging.RawLogDirectory),
            new LogExportService(app.Config.Logging.RawLogDirectory, AppPaths.CrashLogPath));
        _actions = new SettingsFormActions(app, _effects, _connection, _liveFeed);

        // Thin theme apply for workstream A/B panels without rewriting internals.
        UiTheme.ApplyTabChrome(_effects);
        UiTheme.ApplyTabChrome(_preview);

        BuildLayout();
        LoadFromConfig();
        WireEvents();
        _actions.UpdateStatus();
        _statusTimer.Start();
        Shown += (_, _) =>
        {
            PerformLayout();
            _effects.PerformLayout();
            _preview.PerformLayout();
            UpdateFooterTip();
        };
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

        // Sellable tab order: Effects → Preview → Connection → Live feed → Updates
        AddTab("Effects", _effects);
        AddTab("Preview", _preview);
        AddTab("Connection", _connection);
        AddTab("Live feed", _liveFeed);
        AddTab("Updates", BuildUpdatesWrapper());

        _footer.AboutRequested += (_, _) => ShowAbout();
        _tabs.SelectedIndexChanged += (_, _) => UpdateFooterTip();
    }

    private void ApplyInitialBounds()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1080, 800);
        var width = Math.Min(1080, workingArea.Width);
        var height = Math.Min(800, workingArea.Height);
        var left = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
        var top = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(left, top, width, height);
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

    private void LoadFromConfig()
    {
        _effects.LoadConfig(_app.Config.Effects);
        _connection.LoadConfig(_app.Config);
        _liveFeed.ExportIncludeDays = _app.Config.Logging.ExportIncludeDays;
        _preview.RefreshFromEffects();
    }

    private void WireEvents()
    {
        _effects.SaveRequested += (_, _) =>
        {
            if (_actions.Save())
                _preview.RefreshFromEffects();
        };
        _effects.TestRequested += async (_, _) => await _actions.TestSweepAsync();
        _effects.IdleRequested += async (_, _) => await _actions.TestIdleAsync();
        _effects.ProxyToggleRequested += async (_, _) => await _actions.ToggleProxyAsync();
        _effects.PreviewRequested += async (_, args) => await _actions.PreviewEffectAsync(args.Slot);
        _statusTimer.Tick += (_, _) => _actions.UpdateStatus();
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
