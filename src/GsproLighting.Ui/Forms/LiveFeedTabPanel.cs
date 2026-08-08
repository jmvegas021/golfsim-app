using GsproLighting.Core.Config;
using GsproLighting.Core.Contracts;
using GsproLighting.Core.Logging;
using GsproLighting.Core.Models;
using GsproLighting.Ui.Logging;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

public sealed class LiveFeedTabPanel : UserControl
{
    private readonly IShotFeed _feedSource;
    private readonly LogsFolderLauncher _folderLauncher;
    private readonly LogExportService _exportService;
    private readonly ListBox _feed = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 25,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly NumericUpDown _includeDays = new()
    {
        Minimum = 1,
        Maximum = 30,
        Width = 62,
        Value = 1
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 34,
        ForeColor = UiTheme.Muted,
        TextAlign = ContentAlignment.MiddleLeft
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
        _feed.Font = new Font("Cascadia Mono", 9f);
        _feed.DrawItem += DrawFeedItem;
        Controls.Add(_feed);
        Controls.Add(_status);
        Controls.Add(BuildToolbar());

        foreach (var entry in _feedSource.Recent.Reverse())
            _feed.Items.Add(entry);
        _feedSource.EntryAdded += OnFeedEntry;
        Disposed += (_, _) => _feedSource.EntryAdded -= OnFeedEntry;
    }

    public int ExportIncludeDays
    {
        get => (int)_includeDays.Value;
        set => _includeDays.Value = Math.Clamp(value, (int)_includeDays.Minimum, (int)_includeDays.Maximum);
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 50,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var clear = Button("Clear", 78);
        var open = Button("Open logs folder", 142);
        var export = Button("Export logs zip…", 142, primary: true);
        clear.Click += (_, _) => ClearFeed();
        open.Click += (_, _) => OpenLogsFolder();
        export.Click += (_, _) => ExportLogs();
        UiTheme.StyleInput(_includeDays);
        toolbar.Controls.AddRange([
            clear,
            open,
            export,
            new Label
            {
                Text = "Include days",
                AutoSize = false,
                Width = 92,
                Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Muted
            },
            _includeDays
        ]);
        return toolbar;
    }

    private void ClearFeed()
    {
        _feedSource.Clear();
        _feed.Items.Clear();
        ShowStatus("Live feed cleared.");
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
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void DrawFeedItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _feed.Items[e.Index] is not ShotFeedEntry entry)
            return;
        e.DrawBackground();
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
            e.Bounds,
            color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void ShowStatus(string message, bool isError = false)
    {
        _status.Text = message;
        _status.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }

    private static Button Button(string text, int width, bool primary = false)
    {
        var button = new Button { Text = text, Width = width };
        UiTheme.StyleButton(button, primary);
        return button;
    }
}
