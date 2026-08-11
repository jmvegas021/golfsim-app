using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Preview;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// One-tap "home screen" in the style of the WLED app — quick-switch buttons for the 11 bay
/// lighting states, a tap-grid of the connected device's own saved presets, and prominent
/// power/brightness. Purely a UI composition over existing preview playback
/// (<see cref="PreviewPlaybackCoordinator"/>) and device-client (<see cref="WledDeviceClient"/>)
/// code — no new WLED communication logic.
/// </summary>
public sealed partial class QuickControlTabPanel : UserControl
{
    private static readonly HashSet<string> PreviewOnlyIds =
    [
        LightingPreviewIds.Pure,
        LightingPreviewIds.Mishit,
        LightingPreviewIds.Putt,
        LightingPreviewIds.Player,
        LightingPreviewIds.Celebrate,
        LightingPreviewIds.Hazard,
        LightingPreviewIds.Water,
        LightingPreviewIds.OutOfBounds
    ];

    private readonly Func<EffectConfig> _resolveEffects;
    private readonly Func<WledConfig> _resolveWled;
    private readonly Func<string> _getControllerIp;
    private readonly Action<string, string>? _logWledFailure;
    // LedStripPreview's drawing layout is hardcoded for its own default Height (108) — header,
    // pixel bar, legend, and status text are all placed at fixed Y offsets, so shrinking it
    // clips the status line. Do not override Height here.
    private readonly LedStripPreview _strip = new();
    private readonly LightingPreviewCatalog _catalog = new();
    private readonly PreviewPlaybackCoordinator _coordinator;
    private readonly WledDeviceClient _deviceClient = new();
    private readonly FlowLayoutPanel _stateGrid = WrapFlow();
    private readonly FlowLayoutPanel _presetGrid = WrapFlow();
    private readonly EmptyStateBanner _presetsEmpty = new() { Width = 640, Margin = new Padding(0, 4, 0, 4) };
    private readonly Label _status = new();
    private readonly NightCheckBox _power = new() { Text = "Power on" };
    private readonly NightSlider _brightness = new() { Minimum = 1, Maximum = 255, Width = 260 };
    private readonly Label _brightnessValue = new();
    private readonly List<NightButton> _stateButtons = [];
    private int _statusGeneration;
    private CancellationTokenSource? _previewCts;
    private bool _loadingPower;

    public QuickControlTabPanel(
        Func<EffectConfig> resolveEffects,
        Func<WledConfig> resolveWled,
        Func<string> getControllerIp,
        WledPreviewPlayer player,
        Action<string, string>? logWledFailure = null,
        Action? onManualPreviewStarting = null)
    {
        _resolveEffects = resolveEffects;
        _resolveWled = resolveWled;
        _getControllerIp = getControllerIp;
        _logWledFailure = logWledFailure;
        _coordinator = new PreviewPlaybackCoordinator(player, _strip, onManualPreviewStarting);

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 14, 18, 14);
        Font = UiTheme.BodyFont();

        BuildLayout();
        ReloadStateButtons();
        WireEvents();
    }

    public void RefreshFromEffects() => ReloadStateButtons();

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            _ = RefreshPresetsAndPowerAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _deviceClient.Dispose();
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 8, 0)
        };
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 4, 12)
        };
        scroll.Resize += (_, _) =>
        {
            var width = Math.Max(320, scroll.ClientSize.Width - 4);
            if (stack.Width != width)
                stack.Width = width;
        };

        stack.Controls.Add(new TabSectionHeading
        {
            Dock = DockStyle.Top,
            Title = "Quick control",
            Subtitle = "One-tap toggles — like the WLED app's home screen. Full detail lives in " +
                       "Preview and WLED tabs."
        });

        _strip.Dock = DockStyle.Top;
        stack.Controls.Add(_strip);

        _status.Dock = DockStyle.Top;
        _status.Height = UiTheme.TouchMin - 4;
        _status.ForeColor = UiTheme.Muted;
        _status.Font = UiTheme.BodyFont(9.5f);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Margin = new Padding(0, 6, 0, 0);
        _status.Text = "Tap a state to preview it on the real strip.";
        stack.Controls.Add(_status);

        stack.Controls.Add(Section(
            "States",
            "Live bay states: Waiting, Ready, Not Ready. Pure / Mishit / Putt / Celebrate / Hazard and similar quality cues are Preview-only — live shots drive Direction only.",
            _stateGrid));
        stack.Controls.Add(Section("WLED presets", "Your saved presets on this controller.", BuildPresetsBody()));
        stack.Controls.Add(Section("Power", "Applies immediately to the connected controller.", BuildPowerBody()));

        scroll.Controls.Add(stack);
        Controls.Add(scroll);
    }

    private Control BuildPresetsBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        body.Controls.Add(_presetsEmpty);
        body.Controls.Add(_presetGrid);
        _presetsEmpty.ShowMessage("No presets yet", "Refresh once the controller is connected, or save presets in WLED.");
        return body;
    }

    private Control BuildPowerBody()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            BackColor = Color.Transparent
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

        UiTheme.StyleCheckBox(_power);
        _power.Margin = new Padding(0, 6, 0, 0);
        _brightnessValue.AutoSize = true;
        _brightnessValue.ForeColor = UiTheme.Muted;
        _brightnessValue.TextAlign = ContentAlignment.MiddleLeft;
        _brightnessValue.Margin = new Padding(8, 12, 0, 0);

        row.Controls.Add(_power, 0, 0);
        row.Controls.Add(_brightness, 1, 0);
        row.Controls.Add(_brightnessValue, 2, 0);
        return row;
    }

    private static Control Section(string title, string help, Control body)
    {
        var shell = new SurfacePanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, UiTheme.SpacingLg),
            Padding = new Padding(14, 12, 14, 14)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(UiTheme.CreateSectionLabel(title));
        panel.Controls.Add(UiTheme.CreateHelpLabel(help, maxWidth: 780));
        body.Dock = DockStyle.Top;
        panel.Controls.Add(body);
        shell.Controls.Add(panel);
        return shell;
    }

    private static FlowLayoutPanel WrapFlow() => new()
    {
        AutoSize = true,
        WrapContents = true,
        BackColor = Color.Transparent
    };
}
