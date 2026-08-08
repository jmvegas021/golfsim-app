namespace GsproLighting.Ui.Theme;

public static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(15, 20, 18);
    public static readonly Color Panel = Color.FromArgb(24, 33, 28);
    public static readonly Color Border = Color.FromArgb(42, 54, 48);
    public static readonly Color Text = Color.FromArgb(232, 238, 233);
    public static readonly Color Muted = Color.FromArgb(154, 171, 162);
    public static readonly Color Accent = Color.FromArgb(212, 160, 23);
    public static readonly Color Ready = Color.FromArgb(61, 220, 132);
    public static readonly Color NotReady = Color.FromArgb(229, 83, 61);
    public static readonly Color Console = Color.FromArgb(9, 13, 11);

    public static Font BodyFont(float size = 9.5f, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI Variable", size, style);

    public static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(230, 178, 35)
            : Color.FromArgb(34, 46, 39);
        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(186, 137, 14)
            : Console;
        button.BackColor = primary ? Accent : Panel;
        button.ForeColor = primary ? Background : Text;
        button.Height = Math.Max(button.Height, 36);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.TabStop = true;
    }

    public static void StyleInput(Control control)
    {
        control.BackColor = Panel;
        control.ForeColor = Text;
        control.Font = BodyFont();
        control.MinimumSize = new Size(0, 34);
    }

    public static Label CreateSectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        AutoSize = true,
        ForeColor = Muted,
        Font = BodyFont(8.5f, FontStyle.Bold),
        Margin = new Padding(0, 18, 0, 7)
    };
}
