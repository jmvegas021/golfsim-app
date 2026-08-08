using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Ui.Hosting;
using GsproLighting.Ui.Theme;

namespace GsproLighting.Ui.Forms;

internal sealed class SettingsFormActions
{
    private readonly LightingAppCoordinator _app;
    private readonly EffectsTabPanel _effects;
    private readonly ConnectionTabPanel _connection;
    private readonly LiveFeedTabPanel _liveFeed;

    public SettingsFormActions(
        LightingAppCoordinator app,
        EffectsTabPanel effects,
        ConnectionTabPanel connection,
        LiveFeedTabPanel liveFeed)
    {
        _app = app;
        _effects = effects;
        _connection = connection;
        _liveFeed = liveFeed;
    }

    public bool Save()
    {
        try
        {
            var config = _app.Config;
            _effects.ApplyTo(config.Effects);
            _connection.ApplyTo(config);
            config.Logging.ExportIncludeDays = _liveFeed.ExportIncludeDays;
            _app.SaveConfig(config);
            if (config.R50Watch.AutoWatchEnabled && !_app.IsR50WatchRunning)
                _app.StartR50AutoWatch();
            _effects.ShowActionStatus($"Saved {config.Wled.ControllerIp} · {DateTime.Now:t}");
            return true;
        }
        catch (Exception ex)
        {
            _effects.ShowActionStatus(ex.Message, isError: true);
            return false;
        }
    }

    public async Task TestSweepAsync()
    {
        await RunPreviewAsync(
            "Test lights",
            () => _app.Preview.PreviewEffectAsync(_effects.PureSlot, _app.Config.Wled));
    }

    public async Task TestIdleAsync()
    {
        await RunPreviewAsync(
            "Idle glow",
            () => _app.Preview.PreviewEffectAsync(_effects.IdleSlot, _app.Config.Wled));
    }

    public async Task PreviewEffectAsync(EffectSlot slot)
    {
        await RunPreviewAsync(
            "Effect preview",
            () => _app.Preview.PreviewEffectAsync(slot, _app.Config.Wled));
    }

    public async Task ToggleProxyAsync()
    {
        // Proxy start/stop must apply Connection-tab (and other) edits so StartProxy
        // uses current UI values — same as pre-v0.4.7. Preview/test intentionally
        // do NOT call Save(); only this path and the Save button persist config.
        Save();
        if (_app.IsProxyRunning)
            await _app.StopProxyAsync();
        else
            _app.StartProxy();
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        var summary = _app.BuildStatusText();
        var hasError = summary.Contains("error", StringComparison.OrdinalIgnoreCase);
        var (readyText, readyColor) = _app.BallReadyState switch
        {
            BallReadyState.Ready => ("READY", UiTheme.Ready),
            BallReadyState.NotReady => ("NOT READY", UiTheme.NotReady),
            _ => ("WAITING", UiTheme.Accent)
        };
        var isServiceRunning = _app.IsR50WatchRunning || _app.IsProxyRunning;
        var (serviceText, serviceColor) = ResolveServiceStatus(hasError, isServiceRunning);

        _effects.UpdateStatus(
            readyText,
            readyColor,
            serviceText,
            serviceColor,
            summary,
            _app.IsProxyRunning);
    }

    private static (string Text, Color Color) ResolveServiceStatus(bool hasError, bool isRunning) =>
        (hasError, isRunning) switch
        {
            (true, _) => ("SERVICE ERROR", UiTheme.NotReady),
            (false, true) => ("SERVICE ON", UiTheme.Ready),
            _ => ("SERVICE IDLE", UiTheme.Muted)
        };

    private async Task RunPreviewAsync(string label, Func<Task> preview)
    {
        _effects.ShowActionStatus($"{label} playing on-screen and on WLED…");
        try
        {
            await preview();
            _effects.ShowActionStatus($"{label} sent.");
        }
        catch (Exception ex)
        {
            _effects.ShowActionStatus(
                $"On-screen preview played. WLED error: {ex.Message}",
                isError: true);
        }
    }
}
