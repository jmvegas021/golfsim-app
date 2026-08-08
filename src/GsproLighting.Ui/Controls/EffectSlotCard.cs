using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class EffectSlotCard : UserControl
{
    private const int NarrowLayoutWidth = 760;
    private static readonly IReadOnlyDictionary<string, string> AnimationLabels =
        new Dictionary<string, string>
        {
            [EffectAnimations.DirectionAuto] = "Direction marker (auto)",
            [EffectAnimations.CenterToOutside] = "Center → outside",
            [EffectAnimations.OutsideToCenter] = "Outside → center",
            [EffectAnimations.MarkerLeft] = "Marker left",
            [EffectAnimations.MarkerCenter] = "Marker center",
            [EffectAnimations.MarkerRight] = "Marker right",
            [EffectAnimations.Pulse] = "Pulse",
            [EffectAnimations.Flash] = "Flash",
            [EffectAnimations.Sweep] = "Sweep",
            [EffectAnimations.Solid] = "Solid"
        };

    private static readonly IReadOnlyDictionary<int, string> PresetLabels =
        new Dictionary<int, string>
        {
            [89] = "Fireworks · FX 89",
            [23] = "Strobe · FX 23",
            [12] = "Sparkle · FX 12",
            [0] = "Solid · FX 0"
        };

    private readonly ColorSwatchButton _color = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _effect = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly Button _preview = new() { Text = "Preview", Dock = DockStyle.Fill };
    private readonly string _title;
    private readonly string _description;
    private readonly bool _supportsWledPreset;
    private bool _isNarrow;
    private bool _loading;

    public EffectSlotCard(string title, string description, bool supportsWledPreset = false)
    {
        _title = title;
        _description = description;
        _supportsWledPreset = supportsWledPreset;
        Height = 106;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(14, 8, 14, 10);
        BackColor = UiTheme.Panel;
        DoubleBuffered = true;
        TabStop = true;

        _mode.Items.Add("Curated");
        if (_supportsWledPreset)
            _mode.Items.Add("WLED preset");
        UiTheme.StyleInput(_mode);
        UiTheme.StyleInput(_effect);
        UiTheme.StyleButton(_preview);
        _mode.SelectedIndexChanged += (_, _) => RefreshEffectOptions();
        _preview.Click += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);
        MouseEnter += (_, _) => Invalidate();
        MouseLeave += (_, _) => Invalidate();
        Enter += (_, _) => Invalidate();
        Leave += (_, _) => Invalidate();
        ClientSizeChanged += (_, _) => RefreshResponsiveLayout();

        Controls.Add(BuildLayout(isNarrow: false));
    }

    public event EventHandler? PreviewRequested;

    public EffectSlot SelectedSlot
    {
        get
        {
            var mode = _supportsWledPreset && _mode.SelectedIndex == 1
                ? EffectMode.WledPreset
                : EffectMode.Curated;
            return new EffectSlot
            {
                Color = RgbColor.FromRgb(_color.SelectedColor.R, _color.SelectedColor.G, _color.SelectedColor.B),
                Mode = mode,
                Animation = mode == EffectMode.Curated ? SelectedAnimation() : EffectAnimations.Solid,
                WledFxId = mode == EffectMode.WledPreset ? SelectedFxId() : null
            };
        }
        set
        {
            _loading = true;
            _color.SelectedColor = value.Color;
            var canSelectPreset = _supportsWledPreset && value.Mode == EffectMode.WledPreset;
            _mode.SelectedIndex = canSelectPreset ? 1 : 0;
            var displayed = canSelectPreset
                ? value
                : EffectSlot.Curated(value.Color, value.Animation);
            PopulateEffectOptions(displayed);
            _loading = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var isHighlighted = ContainsFocus || ClientRectangle.Contains(PointToClient(Cursor.Position));
        using var pen = new Pen(isHighlighted ? UiTheme.Accent : UiTheme.Border);
        var bounds = ClientRectangle;
        bounds.Width--;
        bounds.Height--;
        e.Graphics.DrawRectangle(pen, bounds);
    }

    private Control BuildLayout(bool isNarrow) =>
        isNarrow ? BuildNarrowLayout() : BuildDesktopLayout();

    private Control BuildDesktopLayout()
    {
        var layout = CreateLayout(5, 1);
        foreach (var width in new[] { 26, 19, 17, 25, 13 })
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        layout.Controls.Add(BuildTitle(), 0, 0);
        layout.Controls.Add(BuildField("Color", _color), 1, 0);
        layout.Controls.Add(BuildField("Mode", _mode), 2, 0);
        layout.Controls.Add(BuildField("Animation", _effect), 3, 0);
        layout.Controls.Add(BuildField("Preview", _preview), 4, 0);
        return layout;
    }

    private Control BuildNarrowLayout()
    {
        var layout = CreateLayout(4, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 23, 22, 36, 19 })
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        var title = BuildTitle();
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 4);
        layout.Controls.Add(BuildField("Color", _color), 0, 1);
        layout.Controls.Add(BuildField("Mode", _mode), 1, 1);
        layout.Controls.Add(BuildField("Animation", _effect), 2, 1);
        layout.Controls.Add(BuildField("Preview", _preview), 3, 1);
        return layout;
    }

    private static TableLayoutPanel CreateLayout(int columns, int rows) => new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = columns,
        RowCount = rows,
        BackColor = UiTheme.Panel
    };

    private Control BuildTitle()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(new Label
        {
            Text = _supportsWledPreset
                ? $"{_description} · Preview only"
                : $"{_description} · Curated live",
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = UiTheme.Muted,
            Font = UiTheme.BodyFont(8.5f)
        });
        panel.Controls.Add(new Label
        {
            Text = _title,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(10.5f, FontStyle.Bold)
        });
        return panel;
    }

    private static Control BuildField(string label, Control input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Margin = new Padding(5, 0, 5, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            ForeColor = UiTheme.Muted,
            Font = UiTheme.BodyFont(8f, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private void RefreshResponsiveLayout()
    {
        var isNarrow = ClientSize.Width < NarrowLayoutWidth;
        if (isNarrow == _isNarrow)
            return;

        _isNarrow = isNarrow;
        SuspendLayout();
        Controls.Clear();
        Height = isNarrow ? 156 : 106;
        Controls.Add(BuildLayout(isNarrow));
        ResumeLayout(performLayout: true);
    }

    private void RefreshEffectOptions()
    {
        if (!_loading)
            PopulateEffectOptions(SelectedSlot);
    }

    private void PopulateEffectOptions(EffectSlot slot)
    {
        _effect.Items.Clear();
        if (_supportsWledPreset && _mode.SelectedIndex == 1)
        {
            foreach (var preset in PresetLabels)
                _effect.Items.Add(preset.Value);
            var fxId = slot.WledFxId ?? 0;
            _effect.Text = PresetLabels.TryGetValue(fxId, out var label) ? label : $"Custom · FX {fxId}";
            return;
        }

        foreach (var animation in AnimationLabels)
            _effect.Items.Add(animation.Value);
        _effect.Text = AnimationLabels.GetValueOrDefault(slot.Animation, slot.Animation);
    }

    private string SelectedAnimation() =>
        AnimationLabels.FirstOrDefault(pair => pair.Value == _effect.Text).Key ?? EffectAnimations.Solid;

    private int SelectedFxId()
    {
        var digits = new string(_effect.Text.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var fxId) ? fxId : 0;
    }
}
