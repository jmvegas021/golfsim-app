using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Hosting;

namespace GsproLighting.Ui.Forms;

public sealed class SettingsForm : Form
{
    private readonly LightingAppCoordinator _app;
    private readonly TextBox _wledIp = new() { Width = 140 };
    private readonly NumericUpDown _wledPort = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly NumericUpDown _ledCount = new() { Minimum = 1, Maximum = 1000, Width = 80 };
    private readonly NumericUpDown _brightness = new() { Minimum = 1, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _listenPort = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly NumericUpDown _upstreamPort = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly NumericUpDown _puttSpeed = new() { Minimum = 1, Maximum = 80, DecimalPlaces = 1, Increment = 0.5M, Width = 80 };
    private readonly NumericUpDown _pureSmash = new() { Minimum = 1, Maximum = 2, DecimalPlaces = 2, Increment = 0.01M, Width = 80 };
    private readonly NumericUpDown _mishitSmash = new() { Minimum = 0.5M, Maximum = 2, DecimalPlaces = 2, Increment = 0.01M, Width = 80 };
    private readonly ColorSwatchButton _idle = new();
    private readonly ColorSwatchButton _pure = new();
    private readonly ColorSwatchButton _mishit = new();
    private readonly ColorSwatchButton _putt = new();
    private readonly ColorSwatchButton _celebrate = new();
    private readonly ColorSwatchButton _hazard = new();
    private readonly ColorSwatchButton _player = new();
    private readonly ListBox _feed = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = new()
    {
        AutoSize = false,
        Height = 56,
        Width = 360,
        Padding = new Padding(0, 8, 0, 0)
    };
    private readonly Button _proxyToggle = new() { Width = 140, Height = 32 };
    private readonly CheckBox _startProxy = new() { Text = "Start Open Connect proxy on launch", AutoSize = true };
    private readonly CheckBox _autoWatch = new() { Text = "Auto-watch R50 / Connect logs (recommended)", AutoSize = true };
    private readonly CheckBox _startMinimized = new() { Text = "Start minimized to tray", AutoSize = true };
    private readonly System.Windows.Forms.Timer _statusTimer;

    public SettingsForm(LightingAppCoordinator app)
    {
        _app = app;
        Text = "GSPro Lighting — Settings";
        Width = 780;
        Height = 680;
        MinimumSize = new Size(700, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        Controls.Add(BuildLayout());
        LoadFromConfig(app.Config);
        UpdateStatusUi();

        _statusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _statusTimer.Tick += (_, _) => UpdateStatusUi();
        _statusTimer.Start();

        app.Feed.EntryAdded += OnFeedEntry;
        app.ProxyStateChanged += OnProxyStateChanged;
        app.R50StatusChanged += OnProxyStateChanged;
        FormClosed += (_, _) =>
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            app.Feed.EntryAdded -= OnFeedEntry;
            app.ProxyStateChanged -= OnProxyStateChanged;
            app.R50StatusChanged -= OnProxyStateChanged;
        };
    }

    private void OnProxyStateChanged()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(UpdateStatusUi);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        root.Controls.Add(BuildSettingsColumn(), 0, 0);
        root.Controls.Add(BuildFeedColumn(), 1, 0);
        return root;
    }

    private Control BuildSettingsColumn()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        panel.Controls.Add(Section("Connection"));
        panel.Controls.Add(Row("WLED IP", _wledIp));
        panel.Controls.Add(Row("WLED UDP port", _wledPort));
        panel.Controls.Add(Row("LED count", _ledCount));
        panel.Controls.Add(Row("Brightness", _brightness));
        panel.Controls.Add(Row("LM listen port", _listenPort));
        panel.Controls.Add(Row("GSPro upstream port", _upstreamPort));

        panel.Controls.Add(Section("Effect colors"));
        panel.Controls.Add(Row("Idle / ready", _idle));
        panel.Controls.Add(Row("Pure strike", _pure));
        panel.Controls.Add(Row("Mishit", _mishit));
        panel.Controls.Add(Row("Putt", _putt));
        panel.Controls.Add(Row("Celebrate", _celebrate));
        panel.Controls.Add(Row("Hazard / OB", _hazard));
        panel.Controls.Add(Row("Player", _player));

        panel.Controls.Add(Section("Thresholds"));
        panel.Controls.Add(Row("Putt max ball speed", _puttSpeed));
        panel.Controls.Add(Row("Pure min smash", _pureSmash));
        panel.Controls.Add(Row("Mishit max smash", _mishitSmash));

        panel.Controls.Add(Section("Startup"));
        panel.Controls.Add(_autoWatch);
        panel.Controls.Add(_startProxy);
        panel.Controls.Add(_startMinimized);

        panel.Controls.Add(BuildActionRow());
        panel.Controls.Add(_status);
        return panel;
    }

    private Control BuildFeedColumn()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
        var title = new Label
        {
            Text = "Live shot feed",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font(Font, FontStyle.Bold)
        };
        var clear = new Button { Text = "Clear", Dock = DockStyle.Bottom, Height = 30 };
        clear.Click += (_, _) =>
        {
            _app.Feed.Clear();
            _feed.Items.Clear();
        };

        panel.Controls.Add(_feed);
        panel.Controls.Add(clear);
        panel.Controls.Add(title);
        foreach (var entry in _app.Feed.Recent.Reverse())
            _feed.Items.Insert(0, FormatEntry(entry));
        return panel;
    }

    private Control BuildActionRow()
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var save = new Button { Text = "Save", Width = 90, Height = 32 };
        var test = new Button { Text = "Test lights", Width = 100, Height = 32 };
        var idle = new Button { Text = "Idle glow", Width = 90, Height = 32 };

        save.Click += (_, _) => Save();
        test.Click += async (_, _) => await TestSweepAsync();
        idle.Click += async (_, _) => await TestIdleAsync();
        _proxyToggle.Click += async (_, _) => await ToggleProxyAsync();

        row.Controls.Add(save);
        row.Controls.Add(test);
        row.Controls.Add(idle);
        row.Controls.Add(_proxyToggle);
        return row;
    }

    private static Label Section(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        Margin = new Padding(0, 14, 0, 4)
    };

    private static Control Row(string label, Control control)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 2)
        };
        row.Controls.Add(new Label
        {
            Text = label,
            Width = 150,
            TextAlign = ContentAlignment.MiddleLeft,
            Height = 28
        });
        row.Controls.Add(control);
        return row;
    }

    private void LoadFromConfig(AppConfig config)
    {
        _wledIp.Text = config.Wled.ControllerIp;
        _wledPort.Value = config.Wled.UdpPort;
        _ledCount.Value = config.Wled.LedCount;
        _brightness.Value = config.Wled.Brightness;
        _listenPort.Value = config.Gspro.ListenPort;
        _upstreamPort.Value = config.Gspro.UpstreamPort;
        _puttSpeed.Value = (decimal)config.Effects.PuttMaxBallSpeedMph;
        _pureSmash.Value = (decimal)config.Effects.PureMinSmashFactor;
        _mishitSmash.Value = (decimal)config.Effects.MishitMaxSmashFactor;
        _idle.SelectedColor = config.Effects.Idle;
        _pure.SelectedColor = config.Effects.PureStrike;
        _mishit.SelectedColor = config.Effects.Mishit;
        _putt.SelectedColor = config.Effects.Putt;
        _celebrate.SelectedColor = config.Effects.Celebrate;
        _hazard.SelectedColor = config.Effects.Hazard;
        _player.SelectedColor = config.Effects.Player;
        _startProxy.Checked = config.Ui.StartProxyOnLaunch;
        _startMinimized.Checked = config.Ui.StartMinimizedToTray;
        _autoWatch.Checked = config.R50Watch.AutoWatchEnabled;
    }

    private AppConfig ReadConfig()
    {
        var config = _app.Config;
        config.Wled.ControllerIp = _wledIp.Text.Trim();
        config.Wled.UdpPort = (int)_wledPort.Value;
        config.Wled.LedCount = (int)_ledCount.Value;
        config.Wled.Brightness = (byte)_brightness.Value;
        config.Gspro.ListenPort = (int)_listenPort.Value;
        config.Gspro.UpstreamPort = (int)_upstreamPort.Value;
        config.Effects.PuttMaxBallSpeedMph = (double)_puttSpeed.Value;
        config.Effects.PureMinSmashFactor = (double)_pureSmash.Value;
        config.Effects.MishitMaxSmashFactor = (double)_mishitSmash.Value;
        config.Effects.Idle = _idle.SelectedColor;
        config.Effects.PureStrike = _pure.SelectedColor;
        config.Effects.Mishit = _mishit.SelectedColor;
        config.Effects.Putt = _putt.SelectedColor;
        config.Effects.Celebrate = _celebrate.SelectedColor;
        config.Effects.Hazard = _hazard.SelectedColor;
        config.Effects.Player = _player.SelectedColor;
        config.Ui.StartProxyOnLaunch = _startProxy.Checked;
        config.Ui.StartMinimizedToTray = _startMinimized.Checked;
        config.R50Watch.AutoWatchEnabled = _autoWatch.Checked;
        return config;
    }

    private void Save()
    {
        try
        {
            var config = ReadConfig();
            _app.SaveConfig(config);
            if (config.R50Watch.AutoWatchEnabled && !_app.IsR50WatchRunning)
                _app.StartR50AutoWatch();
            _status.Text = $"Saved {_app.Config.Wled.ControllerIp} · {DateTime.Now:t}\n{_app.BuildStatusText()}";
            _status.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _status.ForeColor = Color.Firebrick;
        }
    }

    private async Task TestSweepAsync()
    {
        Save();
        _status.Text = "Sending test sweep…";
        _status.ForeColor = Color.DimGray;
        try
        {
            await _app.Preview.PlaySweepAsync(_app.Config.Effects.PureStrike, _app.Config.Wled.LedCount);
            _status.Text = "Test sweep sent\n" + _app.BuildStatusText();
            _status.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            _status.Text = $"WLED error: {ex.Message}";
            _status.ForeColor = Color.Firebrick;
        }
    }

    private async Task TestIdleAsync()
    {
        Save();
        try
        {
            await _app.Preview.PlayIdleGlowAsync(_app.Config.Effects.Idle);
            _status.Text = "Idle glow sent\n" + _app.BuildStatusText();
            _status.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            _status.Text = $"WLED error: {ex.Message}";
            _status.ForeColor = Color.Firebrick;
        }
    }

    private async Task ToggleProxyAsync()
    {
        Save();
        if (_app.IsProxyRunning)
            await _app.StopProxyAsync();
        else
            _app.StartProxy();
        UpdateStatusUi();
    }

    private void UpdateStatusUi()
    {
        if (IsDisposed)
            return;

        _proxyToggle.Text = _app.IsProxyRunning ? "Stop proxy" : "Start proxy";
        var text = _app.BuildStatusText();
        _status.Text = text;
        _status.ForeColor = text.Contains("error", StringComparison.OrdinalIgnoreCase)
            ? Color.Firebrick
            : _app.IsR50WatchRunning || _app.IsProxyRunning
                ? Color.ForestGreen
                : Color.DimGray;
    }

    private void OnFeedEntry(ShotFeedEntry entry)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed)
                    return;
                _feed.Items.Insert(0, FormatEntry(entry));
                while (_feed.Items.Count > 50)
                    _feed.Items.RemoveAt(_feed.Items.Count - 1);
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string FormatEntry(ShotFeedEntry entry) =>
        $"{entry.Timestamp:HH:mm:ss}  [{entry.Kind}]  {entry.Summary}";
}
