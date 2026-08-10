using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class DrgbDirectionMinHoldGateTests
{
    [Fact]
    public void DefaultMinHold_IsFourSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), DrgbDirectionMinHoldGate.DefaultMinHold);
        Assert.Equal(TimeSpan.FromSeconds(4), new DrgbDirectionMinHoldGate().MinHold);
    }

    [Fact]
    public async Task TryDefer_RunsLatestActionAfterHold()
    {
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(80));
        gate.Arm();

        var order = new List<string>();
        var first = gate.TryDefer(
            _ =>
            {
                order.Add("first");
                return Task.CompletedTask;
            },
            CancellationToken.None);
        Assert.NotNull(first);

        var second = gate.TryDefer(
            _ =>
            {
                order.Add("second");
                return Task.CompletedTask;
            },
            CancellationToken.None);
        Assert.NotNull(second);

        await second!;
        Assert.Equal(["second"], order);
        Assert.False(gate.IsHoldActive);
    }

    [Fact]
    public void TryDefer_ReturnsNullWhenHoldInactive()
    {
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromMilliseconds(50));
        Assert.Null(gate.TryDefer(_ => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public void Arm_ClearsDeferredStatus()
    {
        var gate = new DrgbDirectionMinHoldGate(TimeSpan.FromSeconds(2));
        gate.Arm();
        var deferred = gate.TryDefer(_ => Task.CompletedTask, CancellationToken.None);
        Assert.NotNull(deferred);

        gate.Arm();
        Assert.True(gate.IsHoldActive);
    }
}
