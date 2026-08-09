using GsproLighting.Core.Config;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Hosting;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

public sealed class ConnectionTabPanel : UserControl
{
    private readonly EmptyStateBanner _empty = new() { Width = 640, Margin = new Padding(0, 0, 0, 12) };
    private readonly TextBox _wledIp = new() { Width = 220 };
    private readonly NightButton _scan = NightButton.Create("Scan for WLED devices", 200);
    private readonly NightComboBox _scanResults = new() { Width = 260, Visible = false };
    private readonly Label _scanStatus = new()
    {
        AutoSize = true,
        MaximumSize = new Size(560, 0),
        ForeColor = UiTheme.Muted,
        Margin = new Padding(0, 8, 0, 4)
    };
    private CancellationTokenSource? _scanCts;
    private readonly NumericUpDown _wledPort = Number(1, 65535);
    private readonly NumericUpDown _ledCount = Number(1, 1000);
    private readonly NumericUpDown _brightness = Number(1, 255);
    private readonly CheckBox _invert = Check("Invert left / right");
    private readonly NumericUpDown _listenPort = Number(1, 65535);
    private readonly NumericUpDown _upstreamPort = Number(1, 65535);
    private readonly NumericUpDown _puttSpeed = Number(1, 80, 1, 0.5M);
    private readonly NumericUpDown _pureSmash = Number(1, 2, 2, 0.01M);
    private readonly NumericUpDown _mishitSmash = Number(0.5M, 2, 2, 0.01M);
    private readonly NumericUpDown _centerHla = Number(0, 45, 1, 0.1M);
    private readonly CheckBox _autoWatch = Check("Auto-watch R50 / Connect logs (recommended)");
    private readonly CheckBox _startProxy = Check("Start Open Connect proxy on launch");
    private readonly CheckBox _startMinimized = Check("Start minimized to tray");
    private readonly CheckBox _startWithWindows = Check("Start GSPro Lighting when Windows starts");
    private readonly CheckBox _captureDiagnostics = Check("Capture full diagnostics (includes heartbeats)");
    private readonly NightButton _exportConfig = NightButton.Create("Export settings…", 160);
    private readonly NightButton _importConfig = NightButton.Create("Import settings…", 160);
    private readonly Label _backupStatus = new()
    {
        AutoSize = true,
        MaximumSize = new Size(560, 0),
        ForeColor = UiTheme.Muted,
        Margin = new Padding(0, 8, 0, 4)
    };
    private readonly ToolTip _toolTip = new();

    public ConnectionTabPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(22, 12, 22, 20);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        flow.Controls.Add(BuildIntro());
        flow.Controls.Add(_empty);
        flow.Controls.Add(UiTheme.CreateSectionLabel("WLED controller"));
        flow.Controls.Add(Field("Controller IP", _wledIp));
        flow.Controls.Add(BuildScanRow());
        flow.Controls.Add(_scanStatus);
        flow.Controls.Add(Field("UDP port", _wledPort));
        flow.Controls.Add(Field("LED count", _ledCount));
        flow.Controls.Add(Field("Brightness", _brightness));
        flow.Controls.Add(_invert);
        flow.Controls.Add(UiTheme.CreateSectionLabel("GSPro Open Connect"));
        flow.Controls.Add(Field("LM listen port", _listenPort));
        flow.Controls.Add(Field("GSPro upstream port", _upstreamPort));
        flow.Controls.Add(UiTheme.CreateSectionLabel("Shot thresholds"));
        flow.Controls.Add(Field("Putt max ball speed (mph)", _puttSpeed));
        flow.Controls.Add(Field("Pure minimum smash factor", _pureSmash));
        flow.Controls.Add(Field("Mishit maximum smash factor", _mishitSmash));
        flow.Controls.Add(Field("Center HLA ± degrees", _centerHla));
        flow.Controls.Add(UiTheme.CreateSectionLabel("Startup"));
        flow.Controls.Add(_autoWatch);
        flow.Controls.Add(_startProxy);
        flow.Controls.Add(_startMinimized);
        flow.Controls.Add(_startWithWindows);
        flow.Controls.Add(UiTheme.CreateSectionLabel("Diagnostics"));
        flow.Controls.Add(_captureDiagnostics);
        flow.Controls.Add(UiTheme.CreateSectionLabel("Backup & restore"));
        flow.Controls.Add(BuildBackupRow());
        flow.Controls.Add(_backupStatus);
        Controls.Add(flow);

        _wledIp.TextChanged += (_, _) => RefreshEmptyState();
        _autoWatch.CheckedChanged += (_, _) => RefreshEmptyState();
        _scan.Click += async (_, _) => await ScanForWledDevicesAsync();
        _scanResults.SelectedIndexChanged += (_, _) =>
        {
            if (_scanResults.SelectedItem is WledDiscoveredDevice device)
                _wledIp.Text = device.IpAddress;
        };
        UiTheme.StyleCheckBox(_invert);
        UiTheme.StyleCheckBox(_autoWatch);
        UiTheme.StyleCheckBox(_startProxy);
        UiTheme.StyleCheckBox(_startMinimized);
        UiTheme.StyleCheckBox(_startWithWindows);
        UiTheme.StyleCheckBox(_captureDiagnostics);
        _toolTip.SetToolTip(
            _scan,
            "Probes every device on your local network (/24 subnet) for a WLED controller — " +
            "takes a few seconds. Works on the typical flat home/bay network; if your WLED " +
            "device is on a different subnet or VLAN, enter its IP manually instead.");
        _toolTip.SetToolTip(
            _startWithWindows,
            "Adds GSPro Lighting to your Windows user startup so it's already watching for " +
            "GSPro/R50 before you open the sim — combine with \"Start minimized to tray\" for " +
            "zero manual steps.");
        _toolTip.SetToolTip(
            _captureDiagnostics,
            "Logs every raw GSPro/Connect message (including keepalive heartbeats) to the logs " +
            "folder. Off by default to keep log files small. Turn this on before a round you want " +
            "to capture for future analysis (e.g. water hazard / OB / made-putt signals), then use " +
            "Live feed → Export logs zip… afterward. Restart the Open Connect proxy for the " +
            "change to take effect.");
        _toolTip.SetToolTip(
            _exportConfig,
            "Saves all current settings (WLED IP, thresholds, startup options) to a JSON file " +
            "you can copy to another bay/machine.");
        _toolTip.SetToolTip(
            _importConfig,
            "Loads settings from a previously exported JSON file, replacing your current setup.");
        _exportConfig.Click += (_, _) => ExportRequested?.Invoke(this, EventArgs.Empty);
        _importConfig.Click += (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty);
        RefreshEmptyState();
    }

    /// <summary>Raised when the user clicks Export settings — the actual file I/O is handled by
    /// SettingsForm, which owns the full AppConfig and can reload every tab after an import.</summary>
    public event EventHandler? ExportRequested;

    public event EventHandler? ImportRequested;

    public void SetBackupStatus(string message, bool isError = false)
    {
        _backupStatus.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
        _backupStatus.Text = message;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    /// <summary>True when the WLED IP has never been changed from its untouched placeholder.</summary>
    public bool LooksLikeFirstRun => ControllerIp == WledConfig.DefaultControllerIp;

    /// <summary>Kicks off the network scan programmatically (first-run onboarding).</summary>
    public Task TriggerScanAsync() => ScanForWledDevicesAsync();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        base.Dispose(disposing);
    }

    public string ControllerIp => _wledIp.Text.Trim();

    public byte Brightness => (byte)_brightness.Value;

    public int UdpPort => (int)_wledPort.Value;

    public int LedCount => (int)_ledCount.Value;

    public bool InvertLeftRight => _invert.Checked;

    public void LoadConfig(AppConfig config)
    {
        _wledIp.Text = config.Wled.ControllerIp;
        _wledPort.Value = config.Wled.UdpPort;
        _ledCount.Value = config.Wled.LedCount;
        _brightness.Value = config.Wled.Brightness;
        _invert.Checked = config.Wled.InvertLeftRight;
        _listenPort.Value = config.Gspro.ListenPort;
        _upstreamPort.Value = config.Gspro.UpstreamPort;
        _puttSpeed.Value = (decimal)config.Effects.PuttMaxBallSpeedMph;
        _pureSmash.Value = (decimal)config.Effects.PureMinSmashFactor;
        _mishitSmash.Value = (decimal)config.Effects.MishitMaxSmashFactor;
        _centerHla.Value = (decimal)config.Effects.CenterHlaAbsDegrees;
        _autoWatch.Checked = config.R50Watch.AutoWatchEnabled;
        _startProxy.Checked = config.Ui.StartProxyOnLaunch;
        _startMinimized.Checked = config.Ui.StartMinimizedToTray;
        // Reconcile with the actual OS state rather than the last-saved config value — the user
        // may have removed this via Windows' own Startup Apps settings since we last ran.
        _startWithWindows.Checked = WindowsStartupManager.IsRegistered();
        _captureDiagnostics.Checked = config.Logging.LogHeartbeats;
        RefreshEmptyState();
    }

    public void ApplyTo(AppConfig config)
    {
        config.Wled.ControllerIp = _wledIp.Text.Trim();
        config.Wled.UdpPort = (int)_wledPort.Value;
        config.Wled.LedCount = (int)_ledCount.Value;
        config.Wled.Brightness = (byte)_brightness.Value;
        config.Wled.InvertLeftRight = _invert.Checked;
        config.Gspro.ListenPort = (int)_listenPort.Value;
        config.Gspro.UpstreamPort = (int)_upstreamPort.Value;
        config.Effects.PuttMaxBallSpeedMph = (double)_puttSpeed.Value;
        config.Effects.PureMinSmashFactor = (double)_pureSmash.Value;
        config.Effects.MishitMaxSmashFactor = (double)_mishitSmash.Value;
        config.Effects.CenterHlaAbsDegrees = (double)_centerHla.Value;
        config.R50Watch.AutoWatchEnabled = _autoWatch.Checked;
        config.Ui.StartProxyOnLaunch = _startProxy.Checked;
        config.Ui.StartMinimizedToTray = _startMinimized.Checked;
        config.Ui.StartWithWindows = _startWithWindows.Checked;
        WindowsStartupManager.Apply(_startWithWindows.Checked);
        config.Logging.LogHeartbeats = _captureDiagnostics.Checked;
    }

    private void RefreshEmptyState()
    {
        var ip = _wledIp.Text.Trim();
        var missingWled = string.IsNullOrWhiteSpace(ip) || ip is "0.0.0.0";
        if (missingWled)
        {
            _empty.ShowMessage(ProductCopy.NoWledTitle, ProductCopy.NoWledBody);
            return;
        }

        if (!_autoWatch.Checked)
        {
            _empty.ShowMessage(ProductCopy.WaitingR50Title, ProductCopy.WaitingR50Body, waitingAccent: true);
            return;
        }

        _empty.HideMessage();
    }

    private Control BuildScanRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        _scan.Margin = new Padding(0, 0, 10, 0);
        _scanResults.Margin = new Padding(0, 0, 0, 0);
        row.Controls.Add(_scan);
        row.Controls.Add(_scanResults);
        return row;
    }

    private Control BuildBackupRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        _exportConfig.Margin = new Padding(0, 0, 10, 0);
        _importConfig.Margin = new Padding(0, 0, 0, 0);
        row.Controls.Add(_exportConfig);
        row.Controls.Add(_importConfig);
        return row;
    }

    private async Task ScanForWledDevicesAsync()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        var cts = new CancellationTokenSource();
        _scanCts = cts;

        _scan.Enabled = false;
        _scanResults.Visible = false;
        _scanStatus.ForeColor = UiTheme.Muted;
        _scanStatus.Text = "Scanning your network for WLED devices…";

        var progress = new Progress<(int Scanned, int Total)>(p =>
        {
            if (cts.IsCancellationRequested)
                return;
            _scanStatus.Text = $"Scanning your network for WLED devices… {p.Scanned}/{p.Total}";
        });

        using var discovery = new WledNetworkDiscovery();
        try
        {
            var found = await discovery.ScanAsync(progress, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested)
                return;

            if (found.Count == 0)
            {
                _scanStatus.Text = "No WLED devices found on this network — enter the IP manually.";
                return;
            }

            _scanResults.Items.Clear();
            foreach (var device in found)
                _scanResults.Items.Add(device);
            _scanResults.SelectedIndex = 0;
            _scanResults.Visible = true;
            _scanStatus.Text = found.Count == 1
                ? "Found 1 device — select it to fill in the IP."
                : $"Found {found.Count} devices — select one to fill in the IP.";
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                _scanStatus.ForeColor = UiTheme.NotReady;
                _scanStatus.Text = $"Scan failed: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cts))
                _scan.Enabled = true;
        }
    }

    private static Control BuildIntro() =>
        new TabSectionHeading
        {
            Width = 640,
            Title = ProductCopy.ConnectionIntroTitle,
            Subtitle = ProductCopy.ConnectionIntroBody,
            Margin = new Padding(0, 0, 0, 8)
        };

    private static Control Field(string label, Control input)
    {
        UiTheme.StyleInput(input);
        input.MinimumSize = new Size(0, UiTheme.TouchMin - 4);
        var row = new TableLayoutPanel
        {
            Width = 560,
            Height = UiTheme.TouchMin + 4,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.Text,
            BackColor = Color.Transparent
        }, 0, 0);
        row.Controls.Add(input, 1, 0);
        input.Anchor = AnchorStyles.Left;
        return row;
    }

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 560,
        Height = UiTheme.TouchMin,
        ForeColor = UiTheme.Text,
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 2, 0, 2)
    };

    private static NumericUpDown Number(
        decimal minimum,
        decimal maximum,
        int decimals = 0,
        decimal increment = 1) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        DecimalPlaces = decimals,
        Increment = increment,
        Width = 160,
        Height = UiTheme.TouchMin - 4
    };
}
