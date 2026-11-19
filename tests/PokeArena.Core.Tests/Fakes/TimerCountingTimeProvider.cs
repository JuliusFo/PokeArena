using Microsoft.Extensions.Time.Testing;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that counts its timers not yet disposed, so a test can wait until a background
/// loop has armed its next timeout before it advances the clock. Advancing earlier moves time past a timer that does
/// not exist yet; the timer then starts later and never fires (fix/flaky-keepalive-test).
/// </summary>
public sealed class TimerCountingTimeProvider : FakeTimeProvider
{
    private int _active;

    public int ActiveTimers => Volatile.Read(ref _active);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Interlocked.Increment(ref _active);
        return new Counted(base.CreateTimer(callback, state, dueTime, period), this);
    }

    private sealed class Counted(ITimer inner, TimerCountingTimeProvider owner) : ITimer
    {
        private int _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

        public void Dispose()
        {
            Release();
            inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Release();
            return inner.DisposeAsync();
        }

        private void Release()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref owner._active);
            }
        }
    }
}
