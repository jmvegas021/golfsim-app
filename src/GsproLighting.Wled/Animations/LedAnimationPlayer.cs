using GsproLighting.Core.Config;
using GsproLighting.Wled.Contracts;

namespace GsproLighting.Wled.Animations;

public sealed class LedAnimationPlayer
{
    private readonly IWledOutput _output;
    private readonly IReadOnlyDictionary<string, ILedAnimation> _animations;

    public LedAnimationPlayer(IWledOutput output)
    {
        _output = output;
        _animations = CreateAnimations();
    }

    public async Task PlayAsync(
        LedAnimationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = Normalize(request);
        var animation = Resolve(normalized.Animation);

        foreach (var frame in animation.CreateFrames(normalized))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _output.SendPixelsAsync(
                frame.Pixels,
                normalized.Brightness,
                cancellationToken).ConfigureAwait(false);
            if (frame.Duration > TimeSpan.Zero)
                await Task.Delay(frame.Duration, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task PlayAsync(
        EffectSlot slot,
        WledConfig config,
        AnimationDirection direction = AnimationDirection.Center,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(config);
        if (slot.Mode != EffectMode.Curated)
            throw new ArgumentException("The effect slot must use curated mode.", nameof(slot));

        return PlayAsync(new LedAnimationRequest
        {
            Animation = slot.Animation,
            Color = slot.Color,
            LedCount = config.LedCount,
            InvertLeftRight = config.InvertLeftRight,
            Direction = direction,
            Brightness = config.Brightness
        }, cancellationToken);
    }

    public static string ResolveDirectionAnimation(AnimationDirection direction) =>
        direction switch
        {
            AnimationDirection.Left => EffectAnimations.MarkerLeft,
            AnimationDirection.Right => EffectAnimations.MarkerRight,
            _ => EffectAnimations.MarkerCenter
        };

    private ILedAnimation Resolve(string animationId) =>
        _animations.TryGetValue(animationId, out var animation)
            ? animation
            : _animations[EffectAnimations.Solid];

    private static LedAnimationRequest Normalize(LedAnimationRequest request) => new()
    {
        Animation = string.IsNullOrWhiteSpace(request.Animation)
            ? EffectAnimations.Solid
            : request.Animation,
        Color = request.Color,
        LedCount = Math.Max(1, request.LedCount),
        InvertLeftRight = request.InvertLeftRight,
        Direction = request.Direction,
        Brightness = request.Brightness
    };

    private static IReadOnlyDictionary<string, ILedAnimation> CreateAnimations()
    {
        ILedAnimation[] animations =
        [
            new SolidAnimation(),
            new PulseAnimation(),
            new OutsideToCenterAnimation(),
            new CenterToOutsideAnimation(),
            new MarkerAnimation(EffectAnimations.MarkerLeft, AnimationDirection.Left),
            new MarkerAnimation(EffectAnimations.MarkerRight, AnimationDirection.Right),
            new MarkerAnimation(EffectAnimations.MarkerCenter, AnimationDirection.Center),
            new SweepAnimation(),
            new FlashAnimation(),
            new MarkerAnimation(EffectAnimations.DirectionAuto)
        ];
        return animations.ToDictionary(animation => animation.Id, StringComparer.OrdinalIgnoreCase);
    }
}
