using GsproLighting.Core.Config;
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
        if (!Save())
            return;
        await RunPreviewAsync(
            "Test sweep",
            () => _app.Preview.PlaySweepAsync(
                _app.Config.Effects.PureStrike.Color,
                _app.Config.Wled.LedCount));
    }

    public async Task TestIdleAsync()
    {
        if (!Save())
            return;
        await RunPreviewAsync(
            "Idle glow",
            () => _app.Preview.PlayIdleGlowAsync(_app.Config.Effects.Idle.Color));
    }

    public async Task PreviewEffectAsync(EffectSlot slot)
    {
        if (!Save())
            return;
        await RunPreviewAsync(
            "Effect preview",
            () => _app.Preview.PreviewEffectAsync(slot, _app.Config.Wled));
    }

    public async Task ToggleProxyAsync()
    {
        if (!Save())
            return;
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
        var isReady = _app.IsR50WatchRunning || _app.IsProxyRunning;

        var chipText = "IDLE";
        var chipColor = UiTheme.Border;
        if (hasError)
        {
            chipText = "NOT READY";
            chipColor = UiTheme.NotReady;
        }
        else if (isReady)
        {
            chipText = "READY";
            chipColor = UiTheme.Ready;
        }

        _effects.UpdateStatus(
            chipText,
            chipColor,
            summary,
            _app.IsProxyRunning);
    }

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
