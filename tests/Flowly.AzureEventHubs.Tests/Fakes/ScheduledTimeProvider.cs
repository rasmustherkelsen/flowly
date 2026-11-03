namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class ScheduledTimeProvider : TimeProvider
{
    private readonly List<ScheduledTimer> _timers = [];
    private TimeSpan _elapsed;

    public int PendingTimers => _timers.Count;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ScheduledTimer(this, callback, state);
        timer.Change(dueTime, period);

        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        _elapsed += elapsed;

        foreach (var timer in _timers.Where(timer => timer.IsDue(_elapsed)).ToList())
        {
            timer.Fire();
        }
    }

    private sealed class ScheduledTimer(ScheduledTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan? _due;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._elapsed + dueTime;

            if (_due is not null && !owner._timers.Contains(this))
                owner._timers.Add(this);

            return true;
        }

        public bool IsDue(TimeSpan elapsed) => _due is { } due && due <= elapsed;

        public void Fire()
        {
            _due = null;
            owner._timers.Remove(this);
            callback(state);
        }

        public void Dispose() => owner._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
