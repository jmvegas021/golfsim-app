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
    private readonly WledTabManager _manager = new();

    private readonly Label _connectionStatus = new();
    private readonly Label _deviceMeta = new();
    private readonly EmptyStateBanner _empty = new();
    private readonly CheckBox _power = new() { Text = "Power on" };
    private readonly NightSlider _brightness = new() { Minimum = 1, Maximum = 255, Width = 280 };
    private readonly Label _brightnessValue = new();
    private readonly WledCatalogPicker _effects = new() { Height = 220 };
    private readonly WledCatalogPicker _palettes = new() { Height = 220 };
    private readonly NightSlider _speed = new() { Minimum = 0, Maximum = 255, Width = 280 };
    private readonly NightSlider _intensity = new() { Minimum = 0, Maximum = 255, Width = 280 };
    private readonly Label _speedValue = new();
    private readonly Label _intensityValue = new();
    private readonly CheckBox _overlay = new() { Text = "Layered (keep golf flashes over ambient)" };
    private readonly CheckBox _option2 = new() { Text = "Option 2 (effect o2)" };
    private readonly CheckBox _option3 = new() { Text = "Option 3 (effect o3)" };
    private readonly WledColorSlotEditor _colors = new();
    private readonly NightComboBox _segments = new() { Width = 220 };
    private readonly NightComboBox _presets = new() { Width = 280 };
    private readonly NightButton _refresh = new() { Text = "Refresh", Width = 120 };
    private readonly NightButton _apply = new() { Text = "Apply changes", IsPrimary = true, Width = 150 };
    private readonly NightButton _revert = new() { Text = "Revert", Width = 120 };
    private readonly NightButton _ambient = new()
    {
        Text = "Sync ambient\n(Ripple + Red Reef)",
        Width = 168,
        Height = 56,
        WrapText = true
    };
    private readonly NightButton _openWled = new()
    {
        Text = "Open full WLED",
        Width = 148,
        Height = 56,
        WrapText = true
    };
    private readonly NightButton _applyPreset = new() { Text = "Apply preset", Width = 140 };
    private readonly NightButton _playlistNext = new() { Text = "Playlist next", Width = 140 };
    private readonly Label _playlistLabel = new();
    private readonly Panel _editorHost = new() { AutoSize = true, BackColor = Color.Transparent };

    private bool _loading;
    private bool _hasLoaded;
    private int _selectedSegmentId;

    public WledTabPanel(Func<string> getControllerIp, Func<byte> getBrightness)
    {
        _getControllerIp = getControllerIp ?? throw new ArgumentNullException(nameof(getControllerIp));
        _getBrightness = getBrightness ?? throw new ArgumentNullException(nameof(getBrightness));
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
            _manager.Dispose();
        base.Dispose(disposing);
    }
}
