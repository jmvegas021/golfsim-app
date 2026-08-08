using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed class ConnectionTabPanel : UserControl
{
    private readonly TextBox _wledIp = new() { Width = 220 };
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
            WrapContents = false
        };
        flow.Controls.Add(UiTheme.CreateSectionLabel("WLED controller"));
        flow.Controls.Add(Field("Controller IP", _wledIp));
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
        Controls.Add(flow);
    }

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
    }

    private static Control Field(string label, Control input)
    {
        UiTheme.StyleInput(input);
        var row = new TableLayoutPanel { Width = 520, Height = 42, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.Text
        }, 0, 0);
        row.Controls.Add(input, 1, 0);
        input.Anchor = AnchorStyles.Left;
        return row;
    }

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 520,
        Height = 38,
        ForeColor = UiTheme.Text
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
        Width = 150
    };
}
