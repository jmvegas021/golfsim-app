using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Wled;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Primary WLED control surface — scrollable sectioned editor with sticky Apply / Revert.
/// </summary>
public sealed partial class WledTabPanel : UserControl
{
    private readonly Func<string> _getControllerIp;
    private readonly Func<byte> _getBrightness;
    private readonly Action<string, string>? _logWledFailure;
    private readonly WledTabManager _manager = new();

    private readonly Label _connectionStatus = new();
    private readonly Label _deviceMeta = new();
    private readonly EmptyStateBanner _empty = new();
    private readonly NightCheckBox _power = new() { Text = "Power on" };
    private readonly NightSlider _brightness = new() { Minimum = 1, Maximum = 255, Width = 280 };
    private readonly Label _brightnessValue = new();
    private readonly WledCatalogPicker _effects = new() { Height = 220 };
    private readonly WledCatalogPicker _palettes = new() { Height = 220 };
    private readonly NightSlider _speed = new() { Minimum = 0, Maximum = 255, Width = 280 };
    private readonly NightSlider _intensity = new() { Minimum = 0, Maximum = 255, Width = 280 };
    private readonly Label _speedValue = new();
    private readonly Label _intensityValue = new();
    private readonly NightCheckBox _overlay = new() { Text = "Layered (keep golf flashes over ambient)" };
    private readonly NightCheckBox _option2 = new() { Text = "Extra option 1 (o2)" };
    private readonly NightCheckBox _option3 = new() { Text = "Extra option 2 (o3)" };
    private readonly ToolTip _optionsToolTip = new();
    private readonly WledColorSlotEditor _colors = new();
    private readonly NightComboBox _segments = new() { Width = 220 };
    private readonly NightComboBox _presets = new() { Width = 280 };
    private readonly NightButton _refresh = NightButton.Create("Refresh", 120);
    private readonly NightButton _apply = NightButton.Create("Apply changes", 150, isPrimary: true);
    private readonly NightButton _revert = NightButton.Create("Revert", 120);
    private readonly NightButton _ambient = NightButton.Create(
        "Sync ambient\n(Ripple + Red Reef)", 168, height: 56, wrapText: true);
    private readonly NightButton _openWled =
        NightButton.Create("Open full WLED", 148, height: 56, wrapText: true);
    private readonly NightButton _applyPreset = NightButton.Create("Apply preset", 140);
    private readonly NightButton _playlistNext = NightButton.Create("Playlist next", 140);
    private readonly Label _playlistLabel = new();
    private readonly Panel _editorHost = new() { AutoSize = true, BackColor = Color.Transparent };

    private bool _loading;
    private bool _hasLoaded;
    private int _selectedSegmentId;

    public WledTabPanel(
        Func<string> getControllerIp,
        Func<byte> getBrightness,
        Action<string, string>? logWledFailure = null)
    {
        _getControllerIp = getControllerIp ?? throw new ArgumentNullException(nameof(getControllerIp));
        _getBrightness = getBrightness ?? throw new ArgumentNullException(nameof(getBrightness));
        _logWledFailure = logWledFailure;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 12, 18, 12);
        BuildLayout();
        _brightness.Value = Math.Clamp((int)_getBrightness(), 1, 255);
        UpdateValueLabels();
        WireEvents();
        SetEditorEnabled(false);
        ShowEmptyState();
        RefreshConnectionStatus();
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            RefreshConnectionStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager.Dispose();
            _optionsToolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
