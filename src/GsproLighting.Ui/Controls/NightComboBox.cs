using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Controls;

/// <summary>Owner-draw combo matching night-bay panels (Ally-friendly 44px).</summary>
public sealed class NightComboBox : ComboBox
{
    public NightComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = UiTheme.Panel;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();
        ItemHeight = 36;
        IntegralHeight = false;
        MinimumSize = new Size(0, UiTheme.TouchMin);
        Cursor = Cursors.Hand;
        GotFocus += (_, _) => Invalidate();
        LostFocus += (_, _) => Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // WM_PAINT — draw amber focus / border over stock chrome.
        if (m.Msg == 0x000F)
            PaintChrome();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bounds = e.Bounds;
        using var fill = new SolidBrush(selected ? UiTheme.PanelRaised : UiTheme.Panel);
        e.Graphics.FillRectangle(fill, bounds);
        if (selected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            e.Graphics.FillRectangle(accent, bounds.Left, bounds.Top + 4, 3, bounds.Height - 8);
        }

        var text = Items[e.Index]?.ToString() ?? string.Empty;
        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            new Rectangle(bounds.X + 12, bounds.Y, bounds.Width - 16, bounds.Height),
            selected ? UiTheme.Accent : UiTheme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private void PaintChrome()
    {
        using var g = CreateGraphics();
        UiTheme.DrawPanelBorder(g, ClientRectangle, focused: Focused, hovered: false);
        if (Focused)
            UiTheme.DrawFocusRing(g, ClientRectangle, focused: true);
    }
}
