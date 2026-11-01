namespace Flowly.RabbitMQ.Tests.Fakes;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private readonly List<(TimeSpan Duration, TaskCompletionSource Armed)> _waiters = [];
    private readonly Lock _lock = new();
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public int ActiveTimers
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count(timer => timer.IsArmed);
            }
        }
    }

    public override long GetTimestamp()
    {
        lock (_lock)
        {
            return _ticks;
        }
    }

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);

        return timer;
    }

    public Task WhenTimerArmed(TimeSpan duration)
    {
        lock (_lock)
        {
            if (_timers.Any(timer => timer.IsArmed && timer.Duration == duration)) return Task.CompletedTask;

            var armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((duration, armed));

            return armed.Task;
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        var target = GetTimestamp() + elapsed.Ticks;

        while (true)
        {
            ManualTimer? next;
            lock (_lock)
            {
                next = _timers.Where(timer => timer.IsArmed && timer.DueAt <= target).MinBy(timer => timer.DueAt);

                if (next is null)
                {
                    _ticks = target;
                    return;
                }

                _ticks = Math.Max(_ticks, next.DueAt);
            }

            next.Fire();
        }
    }

    private void NotifyArmed(ManualTimer timer)
    {
        List<TaskCompletionSource> ready;
        lock (_lock)
        {
            ready = _waiters.Where(waiter => waiter.Duration == timer.Duration).Select(waiter => waiter.Armed).ToList();
            _waiters.RemoveAll(waiter => waiter.Duration == timer.Duration);
        }

        foreach (var armed in ready)
        {
            armed.TrySetResult();
        }
    }

    private void Register(ManualTimer timer)
    {
        lock (_lock)
        {
            if (!_timers.Contains(timer)) _timers.Add(timer);
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public bool IsArmed { get; private set; }
        public long DueAt { get; private set; }
        public TimeSpan Duration { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                IsArmed = false;
                return true;
            }

            Duration = dueTime;
            DueAt = owner.GetTimestamp() + dueTime.Ticks;
            IsArmed = true;
            owner.Register(this);
            owner.NotifyArmed(this);

            return true;
        }

        public void Fire()
        {
            IsArmed = false;
            callback(state);
        }

        public void Dispose()
        {
            IsArmed = false;
            owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
