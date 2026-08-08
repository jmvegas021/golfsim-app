using GsproLighting.Ui.Updates;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// Settings "Updates" section: version, check, status, install &amp; restart.
/// </summary>
public sealed class UpdatesPanel : UserControl
{
    private readonly AppUpdateService _updates;
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _statusLabel = new()
    {
        AutoSize = false,
        Width = 340,
        Height = 48,
        ForeColor = UiTheme.Muted
    };
    private readonly Button _checkButton = new()
    {
        Text = "Check for updates",
        Width = 150,
        Height = 36
    };
    private readonly Button _installButton = new()
    {
        Text = "Install update & restart",
        Width = 180,
        Height = 36,
        Enabled = false
    };
    private readonly Label _modeLabel = new()
    {
        AutoSize = true,
        ForeColor = UiTheme.Muted,
        Margin = new Padding(0, 2, 0, 6)
    };

    public UpdatesPanel(AppUpdateService updates)
    {
        _updates = updates;
        BackColor = UiTheme.Panel;
        ForeColor = UiTheme.Text;
        Font = UiTheme.BodyFont();
        Padding = new Padding(20);
        BorderStyle = BorderStyle.FixedSingle;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        UiTheme.StyleButton(_checkButton);
        UiTheme.StyleButton(_installButton, primary: true);

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };

        layout.Controls.Add(new Label
        {
            Text = "Updates",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            Font = UiTheme.BodyFont(13f, FontStyle.Bold),
            Margin = new Padding(0, 14, 0, 4)
        });
        layout.Controls.Add(_versionLabel);
        layout.Controls.Add(_modeLabel);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 4)
        };
        buttons.Controls.Add(_checkButton);
        buttons.Controls.Add(_installButton);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_statusLabel);

        Controls.Add(layout);

        _checkButton.Click += async (_, _) => await CheckAsync();
        _installButton.Click += (_, _) => Install();
        _updates.Changed += OnChanged;
        Disposed += (_, _) => _updates.Changed -= OnChanged;
        RefreshFromSnapshot();
    }

    public async Task CheckAsync()
    {
        _checkButton.Enabled = false;
        try
        {
            await _updates.CheckAndDownloadAsync();
        }
        finally
        {
            _checkButton.Enabled = true;
            RefreshFromSnapshot();
        }
    }

    private void Install()
    {
        try
        {
            _installButton.Enabled = false;
            _statusLabel.Text = "Installing update and restarting…";
            _updates.ApplyInstallAndRestart();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            _statusLabel.ForeColor = UiTheme.NotReady;
            RefreshFromSnapshot();
        }
    }

    private void OnChanged()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(RefreshFromSnapshot);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void RefreshFromSnapshot()
    {
        if (IsDisposed)
            return;

        var snap = _updates.Snapshot();
        _versionLabel.Text = $"Current version: v{snap.CurrentVersion}";
        _modeLabel.Text = snap.IsVelopackInstall
            ? "Update source: Velopack · GitHub Releases"
            : "Update source: portable zip · GitHub Releases";
        _statusLabel.Text = snap.StatusText;
        _installButton.Enabled = snap.CanInstall;
        _statusLabel.ForeColor = snap.Phase switch
        {
            UpdatePhase.Error => UiTheme.NotReady,
            UpdatePhase.ReadyToInstall or UpdatePhase.Available => UiTheme.Accent,
            UpdatePhase.UpToDate => UiTheme.Ready,
            UpdatePhase.Downloading or UpdatePhase.Checking => UiTheme.Muted,
            _ => UiTheme.Muted
        };
    }
}
