using GsproLighting.Core.Config;
using GsproLighting.Ui.Controls;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

/// <summary>
/// HTTP-only manual controls for solid colors and the two live ready-state animations.
/// </summary>
public sealed class PreviewTabPanel : UserControl
{
    private readonly Func<string> _resolveControllerIp;
    private readonly Func<byte> _resolveBrightness;
    private readonly Func<int> _resolveLedCount;
    private readonly Action? _cancelLiveEffects;
    private readonly Action<string, string>? _logWledFailure;
    private readonly Label _ipLabel = new();
    private readonly Label _statusLabel = new();
    private readonly WledHttpStateAnimationManager _stateManager;
    private int _statusGeneration;

    public PreviewTabPanel(
        Func<string> resolveControllerIp,
        Func<byte> resolveBrightness,
        Func<int> resolveLedCount,
        WledHttpStateAnimationManager stateManager,
        Action? cancelLiveEffects = null,
        Action<string, string>? logWledFailure = null)
    {
        _resolveControllerIp = resolveControllerIp;
        _resolveBrightness = resolveBrightness;
        _resolveLedCount = resolveLedCount;
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _cancelLiveEffects = cancelLiveEffects;
        _logWledFailure = logWledFailure;

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(18, 14, 18, 14);
        Font = UiTheme.BodyFont();

        Controls.Add(BuildRoot());
        RefreshIpLabel();
    }

    /// <summary>Kept for SettingsForm callers; refreshes the shown controller IP.</summary>
    public void RefreshFromEffects() => RefreshIpLabel();

    protected override void OnPaintBackground(PaintEventArgs e) =>
        UiTheme.FillNightBackground(e.Graphics, ClientRectangle);

    private Control BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(
            new TabSectionHeading
            {
                Dock = DockStyle.Top,
                Title = "Preview HTTP lights",
                Subtitle = "Test solid colors or live ready-state animations. Set the controller IP on Connection first."
            },
            0,
            0);

        _ipLabel.AutoSize = true;
        _ipLabel.ForeColor = UiTheme.Muted;
        _ipLabel.Font = UiTheme.BodyFont(9.5f);
        _ipLabel.Margin = new Padding(0, 8, 0, 12);
        root.Controls.Add(_ipLabel, 0, 1);

        root.Controls.Add(BuildButtons(), 0, 2);

        _statusLabel.AutoSize = true;
        _statusLabel.MaximumSize = new Size(720, 0);
        _statusLabel.ForeColor = UiTheme.Muted;
        _statusLabel.Font = UiTheme.BodyFont(9.5f);
        _statusLabel.Margin = new Padding(0, 16, 0, 0);
        _statusLabel.Text = "Click a color to POST /json/state.";
        root.Controls.Add(_statusLabel, 0, 3);

        return root;
    }

    private Control BuildButtons()
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 0, 0)
        };

        row.Controls.Add(ColorButton("Red", RgbColor.FromRgb(255, 0, 0)));
        row.Controls.Add(ColorButton("Green", RgbColor.FromRgb(0, 255, 0)));
        row.Controls.Add(ColorButton("Blue", RgbColor.FromRgb(0, 0, 255)));
        row.Controls.Add(ColorButton("White", RgbColor.FromRgb(255, 255, 255)));
        row.Controls.Add(OffButton());
        row.Controls.Add(AnimationButton("Not Ready · Breathe", ApplyNotReadyAnimationAsync));
        row.Controls.Add(AnimationButton("Ready · Edges In", ApplyReadyAnimationAsync, isPrimary: true));
        return row;
    }

    private NightButton ColorButton(string label, RgbColor color)
    {
        var button = NightButton.Create(label, 110, isPrimary: label == "Green");
        button.Margin = new Padding(0, 0, 10, 10);
        button.Click += async (_, _) => await ApplyColorAsync(label, color);
        return button;
    }

    private NightButton OffButton()
    {
        var button = NightButton.Create("Off", 110);
        button.Margin = new Padding(0, 0, 10, 10);
        button.Click += async (_, _) => await ApplyOffAsync();
        return button;
    }

    private NightButton AnimationButton(string label, Func<Task> action, bool isPrimary = false)
    {
        var button = NightButton.Create(label, 170, isPrimary: isPrimary);
        button.Margin = new Padding(0, 0, 10, 10);
        button.Click += async (_, _) => await action();
        return button;
    }

    private async Task ApplyColorAsync(string label, RgbColor color)
    {
        var ip = ResolveIpOrStatus();
        if (ip is null)
            return;

        var generation = ++_statusGeneration;
        CancelLiveEffects();
        SetStatus($"Sending {label} to {ip}…");
        try
        {
            await _stateManager.ApplySolidAsync(ip, color, _resolveBrightness()).ConfigureAwait(true);
            if (generation == _statusGeneration)
                SetStatus($"{label} OK · {ip}");
        }
        catch (Exception ex)
        {
            if (generation != _statusGeneration)
                return;
            SetStatus(ex.Message, isError: true);
            _logWledFailure?.Invoke("preview-solid", ex.Message);
        }
    }

    private async Task ApplyOffAsync()
    {
        var ip = ResolveIpOrStatus();
        if (ip is null)
            return;

        var generation = ++_statusGeneration;
        CancelLiveEffects();
        SetStatus($"Sending Off to {ip}…");
        try
        {
            await _stateManager.ApplyOffAsync(ip).ConfigureAwait(true);
            if (generation == _statusGeneration)
                SetStatus($"Off OK · {ip}");
        }
        catch (Exception ex)
        {
            if (generation != _statusGeneration)
                return;
            SetStatus(ex.Message, isError: true);
            _logWledFailure?.Invoke("preview-solid", ex.Message);
        }
    }

    private Task ApplyNotReadyAnimationAsync() =>
        ApplyAnimationAsync(
            "Not Ready breathing",
            (ip, token) => _stateManager.RunRedBreathingAsync(
                ip,
                _resolveBrightness(),
                token));

    private Task ApplyReadyAnimationAsync() =>
        ApplyAnimationAsync(
            "Ready edges-in",
            (ip, token) => _stateManager.RunReadyAsync(
                ip,
                _resolveLedCount(),
                _resolveBrightness(),
                token));

    private async Task ApplyAnimationAsync(
        string label,
        Func<string, CancellationToken, Task> animation)
    {
        var ip = ResolveIpOrStatus();
        if (ip is null)
            return;

        var generation = ++_statusGeneration;
        CancelLiveEffects();
        SetStatus($"Running {label} on {ip}…");
        try
        {
            await animation(ip, CancellationToken.None).ConfigureAwait(true);
            if (generation == _statusGeneration)
                SetStatus($"{label} complete · holding final state");
        }
        catch (OperationCanceledException) when (generation != _statusGeneration)
        {
            // A newer Preview action superseded this animation.
        }
        catch (Exception ex)
        {
            if (generation != _statusGeneration)
                return;
            SetStatus(ex.Message, isError: true);
            _logWledFailure?.Invoke("preview-animation", ex.Message);
        }
    }

    private void CancelLiveEffects()
    {
        try
        {
            _cancelLiveEffects?.Invoke();
        }
        catch
        {
            // Manual Preview must still work if cancellation reporting fails.
        }
    }

    private string? ResolveIpOrStatus()
    {
        RefreshIpLabel();
        var ip = _resolveControllerIp()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(ip) || !WledConfig.IsConfiguredController(ip))
        {
            SetStatus("Set a real controller IP on Connection first.", isError: true);
            return null;
        }

        return ip;
    }

    private void RefreshIpLabel()
    {
        var ip = _resolveControllerIp()?.Trim() ?? "";
        _ipLabel.Text = WledConfig.IsConfiguredController(ip)
            ? $"Controller: {ip}"
            : "Controller: set IP on Connection";
    }

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? UiTheme.NotReady : UiTheme.Muted;
    }
}
