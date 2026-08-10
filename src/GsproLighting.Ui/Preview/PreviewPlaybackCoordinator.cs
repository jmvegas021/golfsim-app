using GsproLighting.Core.Config;
using GsproLighting.Core.Preview;
using GsproLighting.Ui.Controls;
using GsproLighting.Wled;
using GsproLighting.Wled.Animations;

namespace GsproLighting.Ui.Preview;

/// <summary>
/// Coordinates on-screen strip + WLED preview with cancel/skip/supersede and hold-after-play.
/// Does not persist config (no Save).
/// </summary>
public sealed class PreviewPlaybackCoordinator
{
    private static readonly TimeSpan SequenceHold = TimeSpan.FromMilliseconds(1200);

    private readonly WledPreviewPlayer _player;
    private readonly LedStripPreview _strip;
    private readonly PreviewHoldPlanFactory _planFactory = new();
    private readonly LightingPreviewCatalog _catalog = new();
    private readonly Action? _onManualPreviewStarting;
    private CancellationTokenSource? _sequenceCts;
    private CancellationTokenSource? _itemCts;
    private RgbColor? _stripHoldColor;
    private double _stripHoldIntensity = 0.9;

    public PreviewPlaybackCoordinator(
        WledPreviewPlayer player,
        LedStripPreview strip,
        Action? onManualPreviewStarting = null)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _onManualPreviewStarting = onManualPreviewStarting;
    }

    public string? CurrentStateLabel { get; private set; }
    public bool IsPlayAllRunning { get; private set; }

    public async Task PreviewAsync(
        LightingPreviewItem item,
        EffectConfig effects,
        WledConfig wled,
        AnimationDirection direction,
        CancellationToken cancellationToken = default,
        Action? onHoldStarted = null)
    {
        CancelSequence();
        NotifyManualPreviewStarting();
        var plan = _planFactory.Create(item, wled, direction);
        CurrentStateLabel = item.Title;
        await PlayPlanAsync(
                plan,
                wled,
                holdDuration: null,
                cancellationToken,
                onHoldStarted,
                immediate: true)
            .ConfigureAwait(true);
    }

    public async Task PlayAllAsync(
        EffectConfig effects,
        WledConfig wled,
        AnimationDirection direction,
        IProgress<PreviewSequenceProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CancelSequence();
        NotifyManualPreviewStarting();
        _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _sequenceCts.Token;
        IsPlayAllRunning = true;

        try
        {
            var items = _catalog.Create(effects);
            for (var index = 0; index < items.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var item = items[index];
                var isLast = index == items.Count - 1;
                CurrentStateLabel = item.Title;
                progress?.Report(new PreviewSequenceProgress
                {
                    Index = index + 1,
                    Total = items.Count,
                    StateTitle = item.Title
                });

                _itemCts?.Dispose();
                _itemCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var itemToken = _itemCts.Token;
                var plan = _planFactory.Create(item, wled, direction);

                try
                {
                    // Last state keeps indefinite DDP keepalive until Stop / next preview.
                    await PlayPlanAsync(
                            plan,
                            wled,
                            isLast ? null : SequenceHold,
                            itemToken,
                            immediate: true)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Skip current — continue sequence.
                }
            }

            CurrentStateLabel = "Play all complete";
            progress?.Report(new PreviewSequenceProgress
            {
                Index = items.Count,
                Total = items.Count,
                StateTitle = CurrentStateLabel,
                IsComplete = true
            });
        }
        finally
        {
            IsPlayAllRunning = false;
            _itemCts?.Dispose();
            _itemCts = null;
        }
    }

    public async Task StopAsync(EffectConfig effects, WledConfig wled)
    {
        CancelSequence();
        _player.CancelActivePreview();
        CurrentStateLabel = "Stopped";
        _strip.ClearToIdle(effects.Idle.Color);
        _stripHoldColor = effects.Idle.Color;
        _stripHoldIntensity = 0.9;
        // Ambient is restored by the host (ResumeAmbientLighting) — don't start a second Idle
        // hold through the preview player that would race the effect sink.
        await Task.CompletedTask.ConfigureAwait(true);
    }

    public void SkipCurrent()
    {
        if (!IsPlayAllRunning)
            return;
        _itemCts?.Cancel();
        _player.CancelActivePreview();
    }

    public void CancelSequence()
    {
        _itemCts?.Cancel();
        _itemCts?.Dispose();
        _itemCts = null;
        _sequenceCts?.Cancel();
        _sequenceCts?.Dispose();
        _sequenceCts = null;
        IsPlayAllRunning = false;
    }

    private void NotifyManualPreviewStarting()
    {
        try
        {
            _onManualPreviewStarting?.Invoke();
        }
        catch
        {
            // Preview must still run if ambient suspend fails.
        }
    }

    private async Task PlayPlanAsync(
        PreviewHoldPlan plan,
        WledConfig wled,
        TimeSpan? holdDuration,
        CancellationToken cancellationToken,
        Action? onHoldStarted = null,
        bool immediate = false)
    {
        // On-screen strip first so UI feels instant, then WLED (no fade/handoff delay).
        _strip.Play(plan.Slot, plan.Direction, holdAfter: true, holdIntensity: plan.Item.HoldBrightnessFactor);
        _stripHoldColor = plan.Slot.Color;
        _stripHoldIntensity = plan.Item.HoldBrightnessFactor;

        await _player.PreviewAndHoldAsync(
                plan,
                wled,
                holdDuration,
                cancellationToken,
                onHoldStarted,
                skipFadeOut: immediate)
            .ConfigureAwait(true);
    }
}
