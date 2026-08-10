using GsproLighting.Core.Config;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Read-only product lighting legend — display swatches, no editors.</summary>
public sealed class EffectStateLegend : UserControl
{
    private readonly FlowLayoutPanel _rows = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        BackColor = Color.Transparent,
        Margin = new Padding(0),
        Padding = new Padding(0)
    };

    public EffectStateLegend()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Transparent;
        Controls.Add(_rows);
        foreach (var entry in BuildEntries(new EffectConfig()))
            _rows.Controls.Add(new LegendRow(entry));
        _rows.ClientSizeChanged += (_, _) => ResizeRows();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BeginInvoke(ResizeRows);
    }

    private void ResizeRows()
    {
        var width = Math.Max(1, _rows.ClientSize.Width - 2);
        foreach (Control row in _rows.Controls)
            row.Width = width;
    }

    private static IEnumerable<LegendEntry> BuildEntries(EffectConfig effects) =>
    [
        new("Waiting / start", "DDP aqua center→out shimmer (Connect loading / Code 201)", RgbColor.FromRgb(0, 200, 220)),
        new("Not ready", "DDP red expand → full-strip center→out shimmer", RgbColor.FromRgb(255, 0, 0)),
        new("Ready / idle", "DDP sides→center → 28% green center→out shimmer", RgbColor.FromRgb(0, 255, 0)),
        new("Direction L/R", "DDP yellow 28% side band · shimmer toward outer edge", RgbColor.FromRgb(220, 180, 0)),
        new("Direction center", "DDP green 28% center→out shimmer (same zone as Ready)", RgbColor.FromRgb(0, 255, 0)),
        new("Pure", "Bright green · direction marker", effects.PureStrike.Color),
        new("Mishit", "Deep red · direction marker", effects.Mishit.Color),
        new("Putt", "Soft blue · direction marker", effects.Putt.Color),
        new("Player", "Sky blue pulse", effects.Player.Color),
        new("Celebrate", $"Gold fireworks · WLED FX {EffectConfig.CelebrateFxId}", effects.Celebrate.Color),
        new("Hazard", $"Sparkle · WLED FX {EffectConfig.SparkleFxId}", effects.Hazard.Color),
        new("Water", "Teal flash-hold", effects.WaterHazard.Color),
        new("Out of bounds", "Hard red flash-hold", effects.OutOfBounds.Color)
    ];

    private sealed record LegendEntry(string Title, string Description, RgbColor Color);

    private sealed class LegendRow : Control
    {
        private readonly LegendEntry _entry;

        public LegendRow(LegendEntry entry)
        {
            _entry = entry;
            Height = 52;
            Margin = new Padding(0, 0, 0, 8);
            MinimumSize = new Size(200, 48);
            DoubleBuffered = true;
            TabStop = false;
            AccessibleName = entry.Title;
            AccessibleDescription = entry.Description;
            AccessibleRole = AccessibleRole.StaticText;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            UiTheme.FillPanelSurface(g, ClientRectangle, raised: false);

            var swatch = new Rectangle(12, 12, 28, Height - 24);
            var color = Color.FromArgb(_entry.Color.R, _entry.Color.G, _entry.Color.B);
            using (var fill = new SolidBrush(color))
                g.FillRectangle(fill, swatch);
            using (var edge = new Pen(Color.FromArgb(80, 0, 0, 0)))
                g.DrawRectangle(edge, swatch);

            using var titleFont = UiTheme.BodyFont(10f, FontStyle.Bold);
            using var bodyFont = UiTheme.BodyFont(8.25f);
            TextRenderer.DrawText(
                g,
                _entry.Title,
                titleFont,
                new Rectangle(52, 6, Width - 64, 22),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                g,
                _entry.Description,
                bodyFont,
                new Rectangle(52, 26, Width - 64, 20),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            using var border = new Pen(UiTheme.Border);
            var bounds = ClientRectangle;
            bounds.Width--;
            bounds.Height--;
            g.DrawRectangle(border, bounds);
        }
    }
}
