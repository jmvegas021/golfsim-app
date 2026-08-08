using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Wled;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Primary WLED control surface mirroring core WLED app controls via HTTP JSON.
/// </summary>
public sealed partial class WledTabPanel : UserControl
{
    private readonly Func<string> _getControllerIp;
    private readonly Func<byte> _getBrightness;
    private readonly WledTabManager _manager = new();
    private readonly Label _status = new();
    private readonly Label _deviceMeta = new();
    private readonly CheckBox _power = new() { Text = "Power on" };
    private readonly TrackBar _brightness = new() { Minimum = 1, Maximum = 255, TickStyle = TickStyle.None, Width = 280 };
    private readonly Label _brightnessValue = new();
    private readonly WledCatalogPicker _effects = new() { Width = 320, Height = 240 };
    private readonly WledCatalogPicker _palettes = new() { Width = 320, Height = 240 };
    private readonly TrackBar _speed = new() { Minimum = 0, Maximum = 255, TickStyle = TickStyle.None, Width = 220 };
    private readonly TrackBar _intensity = new() { Minimum = 0, Maximum = 255, TickStyle = TickStyle.None, Width = 220 };
    private readonly Label _speedValue = new();
    private readonly Label _intensityValue = new();
    private readonly CheckBox _overlay = new() { Text = "Layered / Overlay (o1)" };
    private readonly CheckBox _option2 = new() { Text = "Option 2 (o2)" };
    private readonly CheckBox _option3 = new() { Text = "Option 3 (o3)" };
    private readonly WledColorSlotEditor _colors = new();
    private readonly NightComboBox _segments = new() { Width = 180 };
    private readonly NightComboBox _presets = new() { Width = 260 };
    private readonly NightButton _refresh = new() { Text = "Refresh", Width = 110 };
    private readonly NightButton _apply = new() { Text = "Apply", IsPrimary = true, Width = 110 };
    private readonly NightButton _revert = new() { Text = "Revert", Width = 110 };
    private readonly NightButton _ambient = new() { Text = "Sync ambient defaults", Width = 180 };
    private readonly NightButton _openWled = new() { Text = "Open full WLED", Width = 140 };
    private readonly NightButton _playlistNext = new() { Text = "Playlist next", Width = 130 };
    private readonly Label _playlistLabel = new();
    private bool _loading;
    private int _selectedSegmentId;

    public WledTabPanel(Func<string> getControllerIp, Func<byte> getBrightness)
    {
        _getControllerIp = getControllerIp ?? throw new ArgumentNullException(nameof(getControllerIp));
        _getBrightness = getBrightness ?? throw new ArgumentNullException(nameof(getBrightness));
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(22, 12, 22, 20);
        BuildLayout();
        _brightness.Value = Math.Clamp((int)_getBrightness(), 1, 255);
        _brightnessValue.Text = _brightness.Value.ToString();
        WireEvents();
        SetStatus("Set controller IP on Connection, then Refresh.");
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _manager.Dispose();
        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 12, 0)
        };

        root.Controls.Add(BuildHeader());
        root.Controls.Add(BuildToolbar());
        root.Controls.Add(BuildPowerRow());
        root.Controls.Add(BuildCatalogRow());
        root.Controls.Add(BuildTimingRow());
        root.Controls.Add(BuildOptionsRow());
        root.Controls.Add(Labeled("Colors", _colors));
        root.Controls.Add(BuildPresetRow());
        root.Controls.Add(BuildFooterNote());
        scroll.Controls.Add(root);
        Controls.Add(scroll);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Height = 72, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        panel.Paint += (_, e) =>
        {
            using var titleFont = UiTheme.HeadingFont(16f, FontStyle.Bold);
            using var bodyFont = UiTheme.BodyFont(9.5f);
            TextRenderer.DrawText(
                e.Graphics,
                "WLED",
                titleFont,
                new Rectangle(0, 0, panel.Width, 28),
                UiTheme.Text,
                TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                e.Graphics,
                ProductCopy.WledTabIntro,
                bodyFont,
                new Rectangle(0, 30, panel.Width - 8, 40),
                UiTheme.Muted,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        };
        return panel;
    }

    private Control BuildToolbar()
    {
        StyleChecks();
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.Controls.Add(_refresh);
        row.Controls.Add(_apply);
        row.Controls.Add(_revert);
        row.Controls.Add(_ambient);
        row.Controls.Add(_openWled);
        _status.AutoSize = true;
        _status.ForeColor = UiTheme.Muted;
        _status.Margin = new Padding(12, 14, 0, 0);
        row.Controls.Add(_status);
        _deviceMeta.AutoSize = true;
        _deviceMeta.ForeColor = UiTheme.Muted;
        _deviceMeta.Margin = new Padding(12, 14, 0, 0);
        row.Controls.Add(_deviceMeta);
        return row;
    }

    private Control BuildPowerRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10)
        };
        row.Controls.Add(_power);
        row.Controls.Add(new Label
        {
            Text = "Brightness",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Margin = new Padding(16, 14, 8, 0)
        });
        row.Controls.Add(_brightness);
        _brightnessValue.AutoSize = true;
        _brightnessValue.ForeColor = UiTheme.Muted;
        _brightnessValue.Margin = new Padding(8, 14, 16, 0);
        row.Controls.Add(_brightnessValue);
        row.Controls.Add(new Label
        {
            Text = "Segment",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Margin = new Padding(8, 14, 8, 0)
        });
        row.Controls.Add(_segments);
        return row;
    }

    private Control BuildCatalogRow()
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.Controls.Add(Labeled("Effect", _effects), 0, 0);
        row.Controls.Add(Labeled("Palette", _palettes), 1, 0);
        return row;
    }

    private Control BuildTimingRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.Controls.Add(SliderBlock("Speed (sx)", _speed, _speedValue));
        row.Controls.Add(SliderBlock("Intensity (ix)", _intensity, _intensityValue));
        return row;
    }

    private Control BuildOptionsRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.Controls.Add(_overlay);
        row.Controls.Add(_option2);
        row.Controls.Add(_option3);
        return row;
    }

    private Control BuildPresetRow()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 8, 0, 8)
        };
        row.Controls.Add(new Label
        {
            Text = "Presets",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Margin = new Padding(0, 14, 8, 0)
        });
        row.Controls.Add(_presets);
        var applyPreset = new NightButton { Text = "Apply preset", Width = 120 };
        applyPreset.Click += async (_, _) => await ApplySelectedPresetAsync();
        row.Controls.Add(applyPreset);
        row.Controls.Add(_playlistNext);
        _playlistLabel.AutoSize = true;
        _playlistLabel.ForeColor = UiTheme.Muted;
        _playlistLabel.Margin = new Padding(12, 14, 0, 0);
        row.Controls.Add(_playlistLabel);
        return row;
    }

    private Control BuildFooterNote() =>
        new Label
        {
            Text = ProductCopy.WledDrgbNote,
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            ForeColor = UiTheme.Muted,
            Margin = new Padding(0, 12, 0, 0)
        };

    private void WireEvents()
    {
        _refresh.Click += async (_, _) => await RefreshAsync();
        _apply.Click += async (_, _) => await ApplyEditorAsync();
        _revert.Click += async (_, _) => await RevertAsync();
        _ambient.Click += async (_, _) => await SyncAmbientAsync();
        _openWled.Click += (_, _) => OpenFullWled();
        _playlistNext.Click += async (_, _) => await PlaylistNextAsync();
        _brightness.ValueChanged += (_, _) => _brightnessValue.Text = _brightness.Value.ToString();
        _speed.ValueChanged += (_, _) => _speedValue.Text = FormatPercent(_speed.Value);
        _intensity.ValueChanged += (_, _) => _intensityValue.Text = FormatPercent(_intensity.Value);
        _segments.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _segments.SelectedItem is not SegmentItem item)
                return;
            _selectedSegmentId = item.Id;
            if (_manager.State is { } state)
            {
                var seg = state.Segments.FirstOrDefault(s => s.Id == item.Id) ?? state.MainSegment;
                BindSegment(seg);
            }
        };
    }

    private void StyleChecks()
    {
        UiTheme.StyleCheckBox(_power);
        UiTheme.StyleCheckBox(_overlay);
        UiTheme.StyleCheckBox(_option2);
        UiTheme.StyleCheckBox(_option3);
    }

    private static Control Labeled(string title, Control content)
    {
        var panel = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 12, 8)
        };
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(9.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 4)
        };
        content.Dock = DockStyle.Top;
        panel.Controls.Add(content);
        panel.Controls.Add(label);
        return panel;
    }

    private static Control SliderBlock(string title, TrackBar bar, Label value)
    {
        var panel = new Panel
        {
            Width = 280,
            Height = 72,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = Color.Transparent
        };
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Location = new Point(0, 0)
        };
        bar.Location = new Point(0, 24);
        value.AutoSize = true;
        value.ForeColor = UiTheme.Muted;
        value.Location = new Point(0, 52);
        panel.Controls.Add(label);
        panel.Controls.Add(bar);
        panel.Controls.Add(value);
        return panel;
    }
}
