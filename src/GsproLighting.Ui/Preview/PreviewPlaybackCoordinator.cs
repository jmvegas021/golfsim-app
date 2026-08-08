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
    private static readonly TimeSpan StripHandoff = TimeSpan.FromMilliseconds(120);

    private readonly WledPreviewPlayer _player;
    private readonly LedStripPreview _strip;
    private readonly PreviewHoldPlanFactory _planFactory = new();
    private readonly LightingPreviewCatalog _catalog = new();
    private CancellationTokenSource? _sequenceCts;
    private CancellationTokenSource? _itemCts;
    private RgbColor? _stripHoldColor;
    private double _stripHoldIntensity = 0.9;

    public PreviewPlaybackCoordinator(WledPreviewPlayer player, LedStripPreview strip)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
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
        var plan = _planFactory.Create(item, wled, direction);
        CurrentStateLabel = item.Title;
        await PlayPlanAsync(plan, wled, holdDuration: null, cancellationToken, onHoldStarted)
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
                    // Last state keeps indefinite DRGB keepalive until Stop / next preview.
                    await PlayPlanAsync(
                            plan,
                            wled,
                            isLast ? null : SequenceHold,
                            itemToken)
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
        CurrentStateLabel = "Stopped · holding ready / idle";
        _strip.ClearToIdle(effects.Idle.Color);
        _stripHoldColor = effects.Idle.Color;
        _stripHoldIntensity = 0.9;
        await _player.StopAndHoldIdleAsync(effects.Idle, wled).ConfigureAwait(true);
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

    private async Task PlayPlanAsync(
        PreviewHoldPlan plan,
        WledConfig wled,
        TimeSpan? holdDuration,
        CancellationToken cancellationToken,
        Action? onHoldStarted = null)
    {
        await HandoffStripAsync(plan, cancellationToken).ConfigureAwait(true);
        _strip.Play(plan.Slot, plan.Direction, holdAfter: true, holdIntensity: plan.Item.HoldBrightnessFactor);
        _stripHoldColor = plan.Slot.Color;
        _stripHoldIntensity = plan.Item.HoldBrightnessFactor;

        await _player.PreviewAndHoldAsync(plan, wled, holdDuration, cancellationToken, onHoldStarted)
            .ConfigureAwait(true);
    }

    private async Task HandoffStripAsync(PreviewHoldPlan _, CancellationToken cancellationToken)
    {
        if (_stripHoldColor is null)
            return;

        _strip.HoldSolid(
            _stripHoldColor,
            intensity: Math.Max(0.12, _stripHoldIntensity * 0.35),
            status: "Transitioning…");
        await Task.Delay(StripHandoff, cancellationToken).ConfigureAwait(true);
    }
}
