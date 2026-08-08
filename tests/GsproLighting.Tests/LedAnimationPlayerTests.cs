using GsproLighting.Core.Config;
using GsproLighting.Wled.Animations;
using GsproLighting.Wled.Contracts;
using Xunit;

namespace GsproLighting.Tests;

public sealed class LedAnimationPlayerTests
{
    private static readonly RgbColor TestColor = RgbColor.FromRgb(120, 60, 30);

    [Theory]
    [InlineData(EffectAnimations.MarkerLeft, 0, 3)]
    [InlineData(EffectAnimations.MarkerCenter, 3, 6)]
    [InlineData(EffectAnimations.MarkerRight, 6, 9)]
    public async Task MarkerAnimation_LightsExpectedRegion(
        string animation,
        int minimumIndex,
        int maximumIndex)
    {
        var output = new RecordingWledOutput();
        var player = new LedAnimationPlayer(output);

        await player.PlayAsync(CreateRequest(animation));

        var litIndices = GetFullColorIndices(output.Frames.Single());
        Assert.NotEmpty(litIndices);
        Assert.All(litIndices, index => Assert.InRange(index, minimumIndex, maximumIndex));
    }

    [Fact]
    public async Task DirectionAuto_InversionMirrorsLeftMarker()
    {
        var normalOutput = new RecordingWledOutput();
        var invertedOutput = new RecordingWledOutput();

        await new LedAnimationPlayer(normalOutput).PlayAsync(
            CreateRequest(EffectAnimations.DirectionAuto, AnimationDirection.Left));
        await new LedAnimationPlayer(invertedOutput).PlayAsync(
            CreateRequest(
                EffectAnimations.DirectionAuto,
                AnimationDirection.Left,
                invertLeftRight: true));

        Assert.True(GetAverageLitIndex(normalOutput.Frames.Single()) < 4.5);
        Assert.True(GetAverageLitIndex(invertedOutput.Frames.Single()) > 4.5);
    }

    [Theory]
    [InlineData(EffectAnimations.OutsideToCenter)]
    [InlineData(EffectAnimations.CenterToOutside)]
    public async Task ChaseAnimations_SendMultipleFrames(string animation)
    {
        var output = new RecordingWledOutput();

        await new LedAnimationPlayer(output).PlayAsync(CreateRequest(animation));

        Assert.Equal(5, output.Frames.Count);
        Assert.All(output.Frames, frame => Assert.Equal(10, frame.Count));
    }

    [Fact]
    public async Task PlayAsync_CancellationFromFirstFrameIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var output = new RecordingWledOutput(() => cancellation.Cancel());
        var player = new LedAnimationPlayer(output);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => player.PlayAsync(
                CreateRequest(EffectAnimations.OutsideToCenter),
                cancellation.Token));

        Assert.Single(output.Frames);
    }

    private static LedAnimationRequest CreateRequest(
        string animation,
        AnimationDirection direction = AnimationDirection.Center,
        bool invertLeftRight = false) =>
        new()
        {
            Animation = animation,
            Color = TestColor,
            LedCount = 10,
            Direction = direction,
            InvertLeftRight = invertLeftRight,
            Brightness = 100
        };

    private static int[] GetFullColorIndices(IReadOnlyList<RgbColor> pixels) =>
        pixels
            .Select((pixel, index) => (pixel, index))
            .Where(item => item.pixel.R == TestColor.R)
            .Select(item => item.index)
            .ToArray();

    private static double GetAverageLitIndex(IReadOnlyList<RgbColor> pixels) =>
        pixels
            .Select((pixel, index) => (pixel, index))
            .Where(item => item.pixel.R > 0)
            .Average(item => item.index);

    private sealed class RecordingWledOutput(Action? onPixelsSent = null) : IWledOutput
    {
        public List<IReadOnlyList<RgbColor>> Frames { get; } = [];

        public void Configure(WledConfig config)
        {
        }

        public Task SendSolidAsync(
            RgbColor color,
            byte? brightness = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendPixelsAsync(
            IReadOnlyList<RgbColor> pixels,
            byte? brightness = null,
            CancellationToken cancellationToken = default)
        {
            Frames.Add(pixels.ToArray());
            onPixelsSent?.Invoke();
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
