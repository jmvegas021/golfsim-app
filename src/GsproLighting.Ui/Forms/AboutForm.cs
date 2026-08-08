using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;
using GsproLighting.Ui.Updates;

namespace GsproLighting.Ui.Forms;

/// <summary>Product About + What’s New + license surface.</summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About GSPro Lighting";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 500);
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();
        Icon = AppIconLoader.AppIcon;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(24),
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.TouchMin + 8));

        root.Controls.Add(BuildBrandRow(), 0, 0);
        root.Controls.Add(new Label
        {
            Text = $"Version v{AppVersionInfo.Current}",
            AutoSize = true,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(10f, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 8),
            BackColor = Color.Transparent
        }, 0, 1);
        root.Controls.Add(new Label
        {
            Text = ProductCopy.BrandSubtitle,
            AutoSize = true,
            ForeColor = UiTheme.Muted,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        }, 0, 2);
        root.Controls.Add(BuildWhatsNew(), 0, 3);
        root.Controls.Add(BuildLicenseBlock(), 0, 4);
        root.Controls.Add(BuildLinkRow(), 0, 5);
        root.Controls.Add(BuildCloseRow(), 0, 6);

        Controls.Add(root);
        Paint += (_, e) => UiTheme.FillNightBackground(e.Graphics, ClientRectangle);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    private static Control BuildBrandRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var mark = new Rectangle(0, 12, 44, 44);
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                mark,
                UiTheme.Accent,
                UiTheme.Ready,
                System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(fill, mark);
            using var led = new SolidBrush(UiTheme.Background);
            for (var i = 0; i < 4; i++)
                g.FillRectangle(led, mark.X + 6 + i * 8, mark.Y + 18, 5, 8);

            using var titleFont = UiTheme.HeadingFont(22f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                "GSPro Lighting",
                titleFont,
                new Rectangle(56, 16, 320, 40),
                UiTheme.Text,
                TextFormatFlags.VerticalCenter);
        };
        return panel;
    }

    private static Control BuildWhatsNew()
    {
        var surface = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 10)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        flow.Controls.Add(new Label
        {
            Text = WhatsNewNotes.Headline,
            AutoSize = true,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(9f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        });
        foreach (var bullet in WhatsNewNotes.Bullets)
        {
            flow.Controls.Add(new Label
            {
                Text = "·  " + bullet,
                AutoSize = false,
                Width = 380,
                Height = 36,
                ForeColor = UiTheme.Text,
                Font = UiTheme.BodyFont(9f),
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            });
        }

        surface.Controls.Add(flow);
        return surface;
    }

    private static Control BuildLicenseBlock()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        panel.Controls.Add(new Label
        {
            Text = "LICENSE",
            AutoSize = true,
            ForeColor = UiTheme.Accent,
            Font = UiTheme.BodyFont(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        });
        panel.Controls.Add(new Label
        {
            Text = ProductCopy.LicenseSummary,
            AutoSize = false,
            Width = 400,
            Height = 52,
            ForeColor = UiTheme.Muted,
            Font = UiTheme.BodyFont(8.5f),
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        });
        panel.Controls.Add(BuildUrlLink(ProductCopy.LicenseLinkLabel, ProductCopy.LicenseUrl));
        return panel;
    }

    private static Control BuildLinkRow()
    {
        return BuildUrlLink(
            $"{ProductCopy.SupportRepoLabel}: {AppUpdateService.RepoUrl}",
            AppUpdateService.RepoUrl);
    }

    private static LinkLabel BuildUrlLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            LinkColor = UiTheme.Accent,
            ActiveLinkColor = UiTheme.AccentHover,
            VisitedLinkColor = UiTheme.Accent,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = Color.Transparent
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore browser launch failures in About.
            }
        };
        return link;
    }

    private Control BuildCloseRow()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent
        };
        var close = new NightButton
        {
            Text = "Close",
            Width = 108,
            IsPrimary = true,
            DialogResult = DialogResult.OK
        };
        AcceptButton = close;
        CancelButton = close;
        row.Controls.Add(close);
        return row;
    }
}
