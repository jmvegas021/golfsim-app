namespace GsproLighting.Ui.Theme;

/// <summary>Night-bay sports-tech design tokens and chrome helpers for GSPro Lighting.</summary>
public static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(11, 16, 14);
    public static readonly Color BackgroundMid = Color.FromArgb(16, 23, 20);
    public static readonly Color Panel = Color.FromArgb(22, 31, 26);
    public static readonly Color PanelRaised = Color.FromArgb(28, 40, 33);
    public static readonly Color Border = Color.FromArgb(42, 58, 50);
    public static readonly Color BorderStrong = Color.FromArgb(58, 78, 68);
    public static readonly Color RimLight = Color.FromArgb(48, 241, 245, 242);
    public static readonly Color Text = Color.FromArgb(241, 245, 242);
    public static readonly Color Muted = Color.FromArgb(154, 171, 162);
    public static readonly Color Accent = Color.FromArgb(212, 160, 23);
    public static readonly Color AccentHover = Color.FromArgb(230, 180, 42);
    public static readonly Color AccentPressed = Color.FromArgb(186, 137, 14);
    public static readonly Color Ready = Color.FromArgb(61, 220, 132);
    public static readonly Color NotReady = Color.FromArgb(229, 83, 61);
    public static readonly Color Console = Color.FromArgb(7, 10, 8);
    public static readonly Color Waiting = Color.FromArgb(180, 120, 20);
    public static readonly Color FocusRing = Color.FromArgb(212, 160, 23);

    public const int TouchMin = 44;
    public const int TransitionMs = 180;
    public const int SpacingXs = 4;
    public const int SpacingSm = 8;
    public const int SpacingMd = 12;
    public const int SpacingLg = 16;
    public const int SpacingXl = 24;

    public static Font HeadingFont(float size = 18f, FontStyle style = FontStyle.Bold) =>
        CreateFont(["Bahnschrift SemiBold Condensed", "Bahnschrift", "Segoe UI Variable Display", "Segoe UI"], size, style);

    public static Font BodyFont(float size = 9.5f, FontStyle style = FontStyle.Regular) =>
        CreateFont(["Segoe UI Variable", "Segoe UI"], size, style);

    public static Font MonoFont(float size = 9f) =>
        CreateFont(["Cascadia Mono", "Consolas", "Courier New"], size, FontStyle.Regular);

    /// <summary>Thin chrome apply for tabs owned by other workstreams.</summary>
    public static void ApplyTabChrome(Control root)
    {
        root.BackColor = Background;
        root.ForeColor = Text;
        root.Font = BodyFont();
    }

    public static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : PanelRaised;
        button.FlatAppearance.MouseDownBackColor = primary ? AccentPressed : Console;
        button.BackColor = primary ? Accent : Panel;
        button.ForeColor = primary ? Background : Text;
        button.MinimumSize = new Size(96, TouchMin);
        button.Height = Math.Max(button.Height, TouchMin);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.TabStop = true;
        button.Font = BodyFont(9.5f, FontStyle.Bold);
        button.Padding = new Padding(12, 0, 12, 0);
    }

    public static void StyleInput(Control control)
    {
        control.BackColor = Panel;
        control.ForeColor = Text;
        control.Font = BodyFont();
        control.MinimumSize = new Size(0, TouchMin - 4);
        if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.GotFocus += (_, _) => textBox.BackColor = PanelRaised;
            textBox.LostFocus += (_, _) => textBox.BackColor = Panel;
        }

        if (control is NumericUpDown numeric)
        {
            numeric.BorderStyle = BorderStyle.FixedSingle;
            numeric.GotFocus += (_, _) => numeric.BackColor = PanelRaised;
            numeric.LostFocus += (_, _) => numeric.BackColor = Panel;
        }
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = Text;
        checkBox.BackColor = Color.Transparent;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.Cursor = Cursors.Hand;
        checkBox.Font = BodyFont();
        checkBox.MinimumSize = new Size(0, TouchMin);
        checkBox.AutoSize = false;
        checkBox.Height = Math.Max(checkBox.Height, TouchMin);
    }

    public static void StyleContextMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Panel;
        menu.ForeColor = Text;
        menu.Font = BodyFont(9.5f);
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new NightMenuRenderer();
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(4, 6, 4, 6);
    }

    public static Label CreateSectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        AutoSize = true,
        ForeColor = Accent,
        Font = BodyFont(8.5f, FontStyle.Bold),
        Margin = new Padding(0, SpacingXl - 4, 0, SpacingSm),
        BackColor = Color.Transparent
    };

    public static void FillNightBackground(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds,
            Background,
            BackgroundMid,
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        graphics.FillRectangle(brush, bounds);
    }

    public static void FillPanelSurface(Graphics graphics, Rectangle bounds, bool raised = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var top = raised ? PanelRaised : Panel;
        var bottom = raised ? Panel : Color.FromArgb(18, 26, 22);
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds,
            top,
            bottom,
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        graphics.FillRectangle(brush, bounds);
        DrawRimLight(graphics, bounds);
    }

    public static void FillInsetWell(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            bounds,
            Console,
            Color.FromArgb(12, 18, 15),
            System.Drawing.Drawing2D.LinearGradientMode.Vertical);
        graphics.FillRectangle(brush, bounds);
        using var edge = new Pen(Border);
        var box = bounds;
        box.Width--;
        box.Height--;
        graphics.DrawRectangle(edge, box);
        using var inset = new Pen(Color.FromArgb(80, 0, 0, 0));
        graphics.DrawLine(inset, bounds.Left + 1, bounds.Top + 1, bounds.Right - 2, bounds.Top + 1);
    }

    public static void DrawRimLight(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width <= 2 || bounds.Height <= 2)
            return;
        using var pen = new Pen(RimLight);
        graphics.DrawLine(pen, bounds.Left + 1, bounds.Top + 1, bounds.Right - 2, bounds.Top + 1);
    }

    public static void DrawPanelBorder(Graphics graphics, Rectangle bounds, bool focused = false, bool hovered = false)
    {
        var color = focused ? FocusRing : (hovered ? BorderStrong : Border);
        using var pen = new Pen(color, focused ? 2f : 1f);
        var box = bounds;
        box.Width--;
        box.Height--;
        graphics.DrawRectangle(pen, box);
    }

    public static void DrawFocusRing(Graphics graphics, Rectangle bounds, bool focused)
    {
        if (!focused)
            return;
        using var pen = new Pen(FocusRing, 2);
        var ring = bounds;
        ring.Inflate(-1, -1);
        ring.Width--;
        ring.Height--;
        graphics.DrawRectangle(pen, ring);
    }

    public static void DrawHoverAmberRing(Graphics graphics, Rectangle bounds, bool active)
    {
        if (!active)
            return;
        using var pen = new Pen(Color.FromArgb(140, Accent.R, Accent.G, Accent.B), 1);
        var ring = bounds;
        ring.Width--;
        ring.Height--;
        graphics.DrawRectangle(pen, ring);
    }

    private static Font CreateFont(string[] families, float size, FontStyle style)
    {
        foreach (var family in families)
        {
            try
            {
                return new Font(family, size, style, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
            }
        }

        return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
    }

    private sealed class NightMenuRenderer : ToolStripProfessionalRenderer
    {
        public NightMenuRenderer() : base(new NightColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Accent : Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.ContentRectangle.Top + e.Item.ContentRectangle.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class NightColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => PanelRaised;
        public override Color MenuItemSelectedGradientBegin => PanelRaised;
        public override Color MenuItemSelectedGradientEnd => Panel;
        public override Color MenuItemBorder => Accent;
        public override Color MenuBorder => BorderStrong;
        public override Color ToolStripDropDownBackground => Panel;
        public override Color ImageMarginGradientBegin => Panel;
        public override Color ImageMarginGradientMiddle => Panel;
        public override Color ImageMarginGradientEnd => Panel;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
