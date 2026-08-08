using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled;

/// <summary>
/// Reusable preview facade for curated animations and WLED effects.
/// </summary>
public sealed class WledPreviewPlayer : IDisposable
{
    private readonly IWledOutput _output;
    private readonly LedAnimationPlayer _animationPlayer;
    private readonly WledHttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public WledPreviewPlayer(IWledOutput output, WledHttpClient? httpClient = null)
    {
        _output = output;
        _animationPlayer = new LedAnimationPlayer(output);
        _httpClient = httpClient ?? new WledHttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public async Task PlayEffectAsync(
        EffectSlot slot,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(config);

        if (slot.Mode == EffectMode.WledPreset)
        {
            if (slot.WledFxId is not int fxId)
                throw new ArgumentException("A WLED preset effect id is required.", nameof(slot));
            await _httpClient.ApplyPresetAsync(config.ControllerIp, fxId, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _animationPlayer.PlayAsync(slot, config, direction, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PreviewEffectAsync(
        EffectSlot slot,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        await PlayEffectAsync(slot, config, direction, cancellationToken).ConfigureAwait(false);
        await Task.Delay(holdDuration ?? TimeSpan.FromMilliseconds(500), cancellationToken)
            .ConfigureAwait(false);
        await _output.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PlaySweepAsync(
        RgbColor color,
        int ledCount,
        CancellationToken cancellationToken = default)
    {
        await _animationPlayer.PlayAsync(new LedAnimationRequest
        {
            Animation = EffectAnimations.Sweep,
            Color = color,
            LedCount = ledCount
        }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        await _output.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PlayIdleGlowAsync(RgbColor color, CancellationToken cancellationToken = default)
    {
        await _output.SendSolidAsync(color, cancellationToken: cancellationToken);
        await Task.Delay(800, cancellationToken);
        await _output.ClearAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
