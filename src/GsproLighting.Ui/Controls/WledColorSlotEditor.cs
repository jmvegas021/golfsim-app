using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Primary / secondary / tertiary RGB slot editor for WLED segments.</summary>
public sealed class WledColorSlotEditor : UserControl
{
    private readonly ColorSwatch _primary = new("Primary");
    private readonly ColorSwatch _secondary = new("Secondary");
    private readonly ColorSwatch _tertiary = new("Tertiary");

    public WledColorSlotEditor()
    {
        Height = UiTheme.TouchComfort + 12;
        BackColor = Color.Transparent;
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        row.Controls.Add(_primary);
        row.Controls.Add(_secondary);
        row.Controls.Add(_tertiary);
        Controls.Add(row);

        _primary.ColorChanged += (_, _) => ColorsChanged?.Invoke(this, EventArgs.Empty);
        _secondary.ColorChanged += (_, _) => ColorsChanged?.Invoke(this, EventArgs.Empty);
        _tertiary.ColorChanged += (_, _) => ColorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ColorsChanged;

    public RgbColor Primary => _primary.Color;
    public RgbColor Secondary => _secondary.Color;
    public RgbColor Tertiary => _tertiary.Color;

    public void SetColors(RgbColor primary, RgbColor secondary, RgbColor tertiary)
    {
        _primary.Color = primary;
        _secondary.Color = secondary;
        _tertiary.Color = tertiary;
    }

    public void MaximizeColors()
    {
        _primary.Color = _primary.Color.WithMaxIntensity();
        _secondary.Color = _secondary.Color.WithMaxIntensity();
        _tertiary.Color = _tertiary.Color.WithMaxIntensity();
        ColorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ColorSwatch : Control
    {
        private RgbColor _color = RgbColor.FromRgb(255, 255, 255);

        public ColorSwatch(string title)
        {
            Text = title;
            Width = 120;
            Height = UiTheme.TouchComfort;
            Margin = new Padding(0, 0, 10, 0);
            Cursor = Cursors.Hand;
            TabStop = true;
            DoubleBuffered = true;
        }

        public event EventHandler? ColorChanged;

        public RgbColor Color
        {
            get => _color;
            set
            {
                _color = RgbColor.FromRgb(value.R, value.G, value.B);
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            using var dialog = new ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(_color.R, _color.G, _color.B),
                FullOpen = true
            };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _color = RgbColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                Invalidate();
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }

            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var fill = System.Drawing.Color.FromArgb(_color.R, _color.G, _color.B);
            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, ClientRectangle);
            UiTheme.DrawPanelBorder(g, ClientRectangle, focused: Focused);
            var luminance = (_color.R * 299 + _color.G * 587 + _color.B * 114) / 1000;
            var text = luminance > 140 ? UiTheme.Background : UiTheme.Text;
            TextRenderer.DrawText(
                g,
                Text,
                UiTheme.BodyFont(9f, FontStyle.Bold),
                ClientRectangle,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
