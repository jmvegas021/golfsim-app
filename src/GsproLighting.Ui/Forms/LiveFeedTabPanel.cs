using GsproLighting.Core.Contracts;
using GsproLighting.Core.Logging;
using GsproLighting.Core.Models;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Logging;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed class LiveFeedTabPanel : UserControl
{
    private readonly IShotFeed _feedSource;
    private readonly LogsFolderLauncher _folderLauncher;
    private readonly LogExportService _exportService;
    private readonly EmptyStateBanner _empty = new()
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0)
    };
    private readonly ListBox _feed = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 28,
        BorderStyle = BorderStyle.None,
        AccessibleName = "Live simulator events"
    };
    private readonly NumericUpDown _includeDays = new()
    {
        Minimum = 1,
        Maximum = 30,
        Width = 72,
        Value = 1
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 36,
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft,
        Text = ProductCopy.LiveFeedWaitingBody
    };
    private readonly Panel _feedWell = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(1),
        BackColor = UiTheme.Border
    };

    public LiveFeedTabPanel(
        IShotFeed feedSource,
        LogsFolderLauncher folderLauncher,
        LogExportService exportService)
    {
        _feedSource = feedSource;
        _folderLauncher = folderLauncher;
        _exportService = exportService;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18);

        _feed.BackColor = UiTheme.Console;
        _feed.ForeColor = UiTheme.Text;
        _feed.Font = UiTheme.MonoFont(9f);
        _feed.DrawItem += DrawFeedItem;
        _feedWell.Paint += (_, e) => UiTheme.FillInsetWell(e.Graphics, _feedWell.ClientRectangle);
        _feedWell.Controls.Add(_feed);
        _feedWell.Controls.Add(_empty);
        Controls.Add(_feedWell);
        Controls.Add(_status);
        Controls.Add(BuildToolbar());

        foreach (var entry in _feedSource.Recent.Reverse())
            _feed.Items.Add(entry);
        RefreshEmptyState();
        _feedSource.EntryAdded += OnFeedEntry;
        Disposed += (_, _) => _feedSource.EntryAdded -= OnFeedEntry;
    }

    public int ExportIncludeDays
    {
        get => (int)_includeDays.Value;
        set => _includeDays.Value = Math.Clamp(value, (int)_includeDays.Minimum, (int)_includeDays.Maximum);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 8)
        };
        var clear = MakeButton("Clear", 96);
        var open = MakeButton("Open logs folder", 160);
        var export = MakeButton("Export logs zip…", 168, primary: true);
        clear.Click += (_, _) => ClearFeed();
        open.Click += (_, _) => OpenLogsFolder();
        export.Click += (_, _) => ExportLogs();
        UiTheme.StyleInput(_includeDays);
        _includeDays.MinimumSize = new Size(72, UiTheme.TouchMin - 4);
        toolbar.Controls.AddRange([
            CreateToolbarTitle(),
            clear,
            open,
            export,
            new Label
            {
                Text = "Include days",
                AutoSize = false,
                Width = 100,
                Height = UiTheme.TouchMin,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Muted,
                BackColor = Color.Transparent
            },
            _includeDays
        ]);
        return toolbar;
    }

    private static Label CreateToolbarTitle() => new()
    {
        Text = "LIVE FEED",
        AutoSize = false,
        Width = 100,
        Height = UiTheme.TouchMin,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = UiTheme.Accent,
        Font = UiTheme.BodyFont(9f, FontStyle.Bold),
        BackColor = Color.Transparent
    };

    private void ClearFeed()
    {
        _feedSource.Clear();
        _feed.Items.Clear();
        RefreshEmptyState();
        ShowStatus("Live feed cleared — waiting for the next Connect event.");
    }

    private void OpenLogsFolder()
    {
        try
        {
            _folderLauncher.Open();
            ShowStatus("Opened local logs folder.");
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
    }

    private void ExportLogs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export GSPro Lighting logs",
            Filter = "Zip archive (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            FileName = $"gspro-lighting-logs-{DateTime.Now:yyyyMMdd}.zip"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var result = _exportService.Export(dialog.FileName, ExportIncludeDays);
            ShowStatus($"Exported {result.ExportedFileNames.Count} file(s) to {result.DestinationPath}");
        }
        catch (Exception ex)
        {
            ShowStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    private void OnFeedEntry(ShotFeedEntry entry)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(() =>
            {
                _feed.Items.Insert(0, entry);
                while (_feed.Items.Count > 50)
                    _feed.Items.RemoveAt(_feed.Items.Count - 1);
                RefreshEmptyState();
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void RefreshEmptyState()
    {
        var waiting = _feed.Items.Count == 0;
        _empty.Visible = waiting;
        _feed.Visible = !waiting;
        if (waiting)
        {
            _empty.ShowMessage(
                ProductCopy.LiveFeedWaitingTitle,
                ProductCopy.WaitingR50Body,
                waitingAccent: true);
            _status.Text = ProductCopy.LiveFeedWaitingBody;
            _status.ForeColor = UiTheme.Muted;
        }
    }

    private void DrawFeedItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _feed.Items[e.Index] is not ShotFeedEntry entry)
            return;

        using var bg = new SolidBrush(e.Index % 2 == 0 ? UiTheme.Console : Color.FromArgb(12, 16, 14));
        e.Graphics.FillRectangle(bg, e.Bounds);
        var color = entry.Kind switch
        {
            "Ready" => UiTheme.Ready,
            "Not ready" => UiTheme.NotReady,
            "Shot" or "Putt" => UiTheme.Accent,
            _ => UiTheme.Text
        };
        TextRenderer.DrawText(
            e.Graphics,
            $"{entry.Timestamp:HH:mm:ss}  [{entry.Kind}]  {entry.Summary}",
            _feed.Font,
            new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height),
            color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if ((e.State & DrawItemState.Focus) != 0)
            UiTheme.DrawFocusRing(e.Graphics, e.Bounds, focused: true);
    }

    private void ShowStatus(string message, bool isError = false)
    {
        _status.Text = message;
        _status.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

    private static NightButton MakeButton(string text, int width, bool primary = false) =>
        new()
        {
            Text = text,
            Width = width,
            IsPrimary = primary,
            Margin = new Padding(0, 0, 8, 0)
        };
}
