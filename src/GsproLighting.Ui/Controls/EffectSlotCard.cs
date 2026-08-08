using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

public sealed class EffectSlotCard : UserControl
{
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

    private readonly ColorSwatchButton _color = new();
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _effect = new() { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
    private readonly Button _preview = new() { Text = "Preview", Width = 92, Height = 36 };
    private bool _loading;

    public EffectSlotCard(string title, string description)
    {
        Height = 88;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(14, 10, 14, 10);
        BackColor = UiTheme.Panel;
        BorderStyle = BorderStyle.FixedSingle;

        _mode.Items.AddRange(["Curated", "WLED preset"]);
        UiTheme.StyleInput(_mode);
        UiTheme.StyleInput(_effect);
        UiTheme.StyleButton(_preview);
        _mode.SelectedIndexChanged += (_, _) => RefreshEffectOptions();
        _preview.Click += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(BuildLayout(title, description));
    }

    public event EventHandler? PreviewRequested;

    public EffectSlot SelectedSlot
    {
        get
        {
            var mode = _mode.SelectedIndex == 1 ? EffectMode.WledPreset : EffectMode.Curated;
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
            _mode.SelectedIndex = value.Mode == EffectMode.WledPreset ? 1 : 0;
            PopulateEffectOptions(value);
            _loading = false;
        }
    }

    private Control BuildLayout(string title, string description)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        layout.Controls.Add(BuildTitle(title, description), 0, 0);
        layout.Controls.Add(_color, 1, 0);
        layout.Controls.Add(_mode, 2, 0);
        layout.Controls.Add(_effect, 3, 0);
        layout.Controls.Add(_preview, 4, 0);
        CenterChildren(layout);
        return layout;
    }

    private static Control BuildTitle(string title, string description)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(new Label
        {
            Text = description,
            Dock = DockStyle.Bottom,
            Height = 25,
            ForeColor = UiTheme.Muted,
            Font = UiTheme.BodyFont(8.5f)
        });
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 27,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(10.5f, FontStyle.Bold)
        });
        return panel;
    }

    private static void CenterChildren(TableLayoutPanel layout)
    {
        foreach (Control control in layout.Controls)
            control.Anchor = control is Panel ? AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.None;
    }

    private void RefreshEffectOptions()
    {
        if (_loading)
            return;
        PopulateEffectOptions(SelectedSlot);
    }

    private void PopulateEffectOptions(EffectSlot slot)
    {
        _effect.Items.Clear();
        if (_mode.SelectedIndex == 1)
        {
            foreach (var preset in PresetLabels)
                _effect.Items.Add(preset.Value);
            var fxId = slot.WledFxId ?? 0;
            _effect.Text = PresetLabels.TryGetValue(fxId, out var label) ? label : $"Custom · FX {fxId}";
        }
        else
        {
            foreach (var animation in AnimationLabels)
                _effect.Items.Add(animation.Value);
            _effect.Text = AnimationLabels.GetValueOrDefault(slot.Animation, slot.Animation);
        }
    }

    private string SelectedAnimation() =>
        AnimationLabels.FirstOrDefault(pair => pair.Value == _effect.Text).Key ?? EffectAnimations.Solid;

    private int SelectedFxId()
    {
        var digits = new string(_effect.Text.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var fxId) ? fxId : 0;
    }
}
