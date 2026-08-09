namespace GsproLighting.Gspro.Dispatch;

/// <summary>
/// Fires a shot-event-sink call in the background instead of awaiting it inline.
/// <c>WledShotEffectSink</c>'s "hold" effects (Idle/Waiting/NotReady) run indefinitely until
/// superseded by the next sink call — awaiting one synchronously from a message/line read loop
/// blocks that loop from ever reading the next event needed to supersede/end the hold, which
/// permanently stalls the R50 log tail or GSPro proxy pipe. The sink's own internal gate
/// (cancel-then-replace on each new call) already makes concurrent/overlapping dispatch safe;
/// the caller just needs to stop blocking on completion.
/// </summary>
public static class SinkCallDispatcher
{
    public static void Fire(Func<Task> call, Action<string> onError)
    {
        _ = RunAsync(call, onError);
    }

    private static async Task RunAsync(Func<Task> call, Action<string> onError)
    {
        try
        {
            await call().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer live event via the sink's own gate, or real shutdown —
            // nothing to log; the sink itself already logs genuine effect failures.
        }
        catch (Exception ex)
        {
            try
            {
                onError(ex.Message);
            }
            catch
            {
                // Logging must never break the caller.
            }
        }
    }
}
