namespace GsproLighting.Wled.Device;

/// <summary>
/// Keeps hit-direction DDP poses visible for a minimum duration before Ready /
/// Not Ready / Waiting may supersede. Latest deferred status wins; a new
/// direction arms a fresh hold and drops any pending status.
/// <para>
/// Timing: <see cref="DefaultMinHold"/> (4s) with no extra grace. When the gate
/// releases and a deferred action is queued (including the synthesized Not Ready
/// fallback armed with each direction), that action runs immediately. If
/// Cancel/Waiting clears the gate first, the deferred work is dropped.
/// </para>
/// </summary>
public sealed class DrgbDirectionMinHoldGate
{
    /// <summary>
    /// Default minimum visible time for Left / Center / Right hit cues.
    /// After this elapses with no Ready/Not Ready queued, the direction
    /// controller synthesizes Not Ready (no additional grace period).
    /// </summary>
    public static readonly TimeSpan DefaultMinHold = TimeSpan.FromSeconds(4);

    private readonly TimeSpan _minHold;
    private readonly object _gate = new();
    private DateTimeOffset _holdUntilUtc = DateTimeOffset.MinValue;
    private int _generation;
    private Func<CancellationToken, Task>? _deferred;
    private Task? _waiter;

    public DrgbDirectionMinHoldGate(TimeSpan? minHold = null) =>
        _minHold = minHold ?? DefaultMinHold;

    public TimeSpan MinHold => _minHold;

    public bool IsHoldActive
    {
        get
        {
            lock (_gate)
                return IsHoldActiveUnlocked();
        }
    }

    /// <summary>Starts a new direction min-hold window and clears any deferred status.</summary>
    public void Arm()
    {
        lock (_gate)
            ResetUnlocked(DateTimeOffset.UtcNow + _minHold);
    }

    /// <summary>Cancels hold tracking and drops deferred work (manual cancel / dispose).</summary>
    public void Clear()
    {
        lock (_gate)
            ResetUnlocked(DateTimeOffset.MinValue);
    }

    /// <summary>
    /// When a direction min-hold is active, queues <paramref name="action"/> (latest wins)
    /// and returns a task that runs it after the hold. Otherwise returns null.
    /// </summary>
    public Task? TryDefer(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_gate)
        {
            if (!IsHoldActiveUnlocked())
                return null;

            _deferred = action;
            _waiter ??= WaitAndFlushAsync(cancellationToken);
            return _waiter;
        }
    }

    private bool IsHoldActiveUnlocked() => DateTimeOffset.UtcNow < _holdUntilUtc;

    private void ResetUnlocked(DateTimeOffset holdUntilUtc)
    {
        _holdUntilUtc = holdUntilUtc;
        _generation++;
        _deferred = null;
    }

    private async Task WaitAndFlushAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            int generation;
            lock (_gate)
            {
                generation = _generation;
                delay = _holdUntilUtc - DateTimeOffset.UtcNow;
            }

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Clear();
                    throw;
                }
            }

            Func<CancellationToken, Task>? pending;
            lock (_gate)
            {
                // Arm/Clear raced with this waiter — restart or exit instead of flushing
                // against a stale generation.
                if (generation != _generation)
                {
                    if (_deferred is null)
                    {
                        _waiter = null;
                        return;
                    }

                    if (IsHoldActiveUnlocked())
                        continue;
                }

                pending = _deferred;
                _deferred = null;
                _holdUntilUtc = DateTimeOffset.MinValue;
                _waiter = null;
            }

            if (pending is null)
                return;

            await pending(cancellationToken).ConfigureAwait(false);
            return;
        }
    }
}
