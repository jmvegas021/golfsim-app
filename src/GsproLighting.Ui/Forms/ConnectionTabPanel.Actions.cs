using GsproLighting.Core.Config;
using GsproLighting.Ui.Hosting;
using GsproLighting.Ui.Theme;
using GsproLighting.Wled.Device;

namespace GsproLighting.Ui.Forms;

public sealed partial class ConnectionTabPanel
{
    public void LoadConfig(AppConfig config)
    {
        _suppressControllerIpCommit = true;
        try
        {
            _wledIp.Text = config.Wled.ControllerIp;
            _wledPort.Value = config.Wled.UdpPort;
            _ledCountValue = Math.Max(1, config.Wled.LedCount);
            _brightnessValue = config.Wled.Brightness == 0 ? (byte)1 : config.Wled.Brightness;
            _invert.Checked = config.Wled.InvertLeftRight;
            _listenPort.Value = config.Gspro.ListenPort;
            _upstreamPort.Value = config.Gspro.UpstreamPort;
            _puttSpeed.Value = (decimal)config.Effects.PuttMaxBallSpeedMph;
            _pureSmash.Value = (decimal)config.Effects.PureMinSmashFactor;
            _mishitSmash.Value = (decimal)config.Effects.MishitMaxSmashFactor;
            _centerHla.Value = (decimal)config.Effects.CenterHlaAbsDegrees;
            _autoWatch.Checked = config.R50Watch.AutoWatchEnabled;
            _startProxy.Checked = config.Ui.StartProxyOnLaunch;
            _startMinimized.Checked = config.Ui.StartMinimizedToTray;
            // Reconcile with the actual OS state rather than the last-saved config value — the user
            // may have removed this via Windows' own Startup Apps settings since we last ran.
            _startWithWindows.Checked = WindowsStartupManager.IsRegistered();
            _captureDiagnostics.Checked = config.Logging.LogHeartbeats;
            UpdateDeviceFromControllerLabel(savedOnly: true);
            RefreshEmptyState();
        }
        finally
        {
            _suppressControllerIpCommit = false;
        }
    }

    public void ApplyTo(AppConfig config)
    {
        config.Wled.ControllerIp = _wledIp.Text.Trim();
        config.Wled.UdpPort = (int)_wledPort.Value;
        config.Wled.LedCount = _ledCountValue;
        config.Wled.Brightness = _brightnessValue;
        config.Wled.InvertLeftRight = _invert.Checked;
        config.Gspro.ListenPort = (int)_listenPort.Value;
        config.Gspro.UpstreamPort = (int)_upstreamPort.Value;
        config.Effects.PuttMaxBallSpeedMph = (double)_puttSpeed.Value;
        config.Effects.PureMinSmashFactor = (double)_pureSmash.Value;
        config.Effects.MishitMaxSmashFactor = (double)_mishitSmash.Value;
        config.Effects.CenterHlaAbsDegrees = (double)_centerHla.Value;
        config.R50Watch.AutoWatchEnabled = _autoWatch.Checked;
        config.Ui.StartProxyOnLaunch = _startProxy.Checked;
        config.Ui.StartMinimizedToTray = _startMinimized.Checked;
        config.Ui.StartWithWindows = _startWithWindows.Checked;
        WindowsStartupManager.Apply(_startWithWindows.Checked);
        config.Logging.LogHeartbeats = _captureDiagnostics.Checked;
    }

    private async Task CommitControllerFromDeviceAsync()
    {
        if (_suppressControllerIpCommit || string.IsNullOrWhiteSpace(ControllerIp))
            return;

        _pullCts?.Cancel();
        _pullCts?.Dispose();
        var cts = new CancellationTokenSource();
        _pullCts = cts;

        _scanStatus.ForeColor = UiTheme.Muted;
        _scanStatus.Text = "Reading LED count and brightness from controller…";
        using var reader = new WledConnectionSnapshotReader();
        try
        {
            var snapshot = await reader.ReadAsync(ControllerIp, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested)
                return;

            ApplyDeviceSnapshot(snapshot);
            _scanStatus.Text = $"Controller ready — {snapshot.LedCount} LEDs · brightness {snapshot.Brightness}.";
            ControllerIpCommitted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Superseded by another commit/scan.
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested)
                return;
            _scanStatus.ForeColor = UiTheme.NotReady;
            _scanStatus.Text = $"Couldn't read strip details ({ex.Message}). IP still saved for live lighting.";
            UpdateDeviceFromControllerLabel(savedOnly: true);
            ControllerIpCommitted?.Invoke();
        }
    }

    private void ApplyDeviceSnapshot(WledConnectionSnapshot snapshot)
    {
        _ledCountValue = snapshot.LedCount;
        _brightnessValue = snapshot.Brightness;
        UpdateDeviceFromControllerLabel(
            deviceName: snapshot.DeviceName,
            version: snapshot.Version);
    }

    private void UpdateDeviceFromControllerLabel(
        bool savedOnly = false,
        string? deviceName = null,
        string? version = null)
    {
        if (savedOnly)
        {
            _deviceFromController.Text =
                $"Using saved strip settings: {_ledCountValue} LEDs · brightness {_brightnessValue} " +
                "(refresh by leaving the IP field or scanning).";
            return;
        }

        var name = string.IsNullOrWhiteSpace(deviceName) ? "Controller" : deviceName;
        var ver = string.IsNullOrWhiteSpace(version) ? "" : $" · v{version}";
        _deviceFromController.Text =
            $"{name}{ver} — {_ledCountValue} LEDs · brightness {_brightnessValue} (from device).";
    }

    private void RefreshEmptyState()
    {
        var ip = _wledIp.Text.Trim();
        var missingWled = string.IsNullOrWhiteSpace(ip) || ip is "0.0.0.0";
        if (missingWled)
        {
            _empty.ShowMessage(ProductCopy.NoWledTitle, ProductCopy.NoWledBody);
            return;
        }

        if (!_autoWatch.Checked)
        {
            _empty.ShowMessage(ProductCopy.WaitingR50Title, ProductCopy.WaitingR50Body, waitingAccent: true);
            return;
        }

        _empty.HideMessage();
    }

    private async Task ScanForWledDevicesAsync()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        var cts = new CancellationTokenSource();
        _scanCts = cts;

        _scan.Enabled = false;
        _scanResults.Visible = false;
        _scanStatus.ForeColor = UiTheme.Muted;
        _scanStatus.Text = "Scanning your network for WLED devices…";

        var progress = new Progress<(int Scanned, int Total)>(p =>
        {
            if (cts.IsCancellationRequested)
                return;
            _scanStatus.Text = $"Scanning your network for WLED devices… {p.Scanned}/{p.Total}";
        });

        using var discovery = new WledNetworkDiscovery();
        try
        {
            var found = await discovery.ScanAsync(progress, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested)
                return;

            if (found.Count == 0)
            {
                _scanStatus.Text =
                    "No WLED devices found on this network — enter the IP manually. " +
                    "Scan probes each /24 LAN around this PC’s adapters (HTTP /json/info).";
                return;
            }

            _scanResults.Items.Clear();
            foreach (var device in found)
                _scanResults.Items.Add(device);
            // Selecting index 0 commits IP + pulls brightness/LED count via SelectedIndexChanged.
            _scanResults.SelectedIndex = 0;
            _scanResults.Visible = true;
            if (_scanStatus.ForeColor != UiTheme.NotReady)
            {
                var ip = found[0].IpAddress;
                _scanStatus.Text = found.Count == 1
                    ? $"Found 1 device at {ip} — reading strip details…"
                    : $"Found {found.Count} devices — using {ip}; pick another to switch.";
            }
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                _scanStatus.ForeColor = UiTheme.NotReady;
                _scanStatus.Text = $"Scan failed: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cts))
                _scan.Enabled = true;
        }
    }
}
