using GsproLighting.Gspro.Dispatch;
using Xunit;

namespace GsproLighting.Tests;

public sealed class SinkCallDispatcherTests
{
    [Fact]
    public void Fire_ReturnsImmediately_EvenWhenCallNeverCompletes()
    {
        // Mirrors WledShotEffectSink's indefinite Idle/Waiting/NotReady holds — Fire must not
        // block the caller (the R50 tail/proxy read loop) waiting for these to finish.
        var neverCompletes = new TaskCompletionSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        SinkCallDispatcher.Fire(() => neverCompletes.Task, _ => { });

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500, "Fire must not block on the dispatched call.");
    }

    [Fact]
    public async Task Fire_OnFailure_InvokesOnErrorWithMessage()
    {
        var errors = new List<string>();
        var signal = new TaskCompletionSource();

        SinkCallDispatcher.Fire(
            () => Task.FromException(new InvalidOperationException("boom")),
            msg =>
            {
                errors.Add(msg);
                signal.TrySetResult();
            });

        await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("boom", errors);
    }

    [Fact]
    public async Task Fire_OnCancellation_DoesNotInvokeOnError()
    {
        var errors = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        SinkCallDispatcher.Fire(() => Task.FromCanceled(cts.Token), errors.Add);

        // Superseded/cancelled dispatches are expected and silent — give the background
        // continuation a moment to run, then confirm nothing was reported as an error.
        await Task.Delay(200);
        Assert.Empty(errors);
    }
}
