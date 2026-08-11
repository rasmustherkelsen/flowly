using System.Text;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.InMemory;

internal sealed class InMemoryStreamLog(StreamRetentionSettings retention, bool referencePassingEnabled)
{
    private readonly Lock _lock = new();
    private readonly List<InMemoryEnvelope> _entries = [];
    private long _baseOffset;
    private long _retainedBytes;
    private TaskCompletionSource _appendSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public long BaseOffset
    {
        get
        {
            lock (_lock)
            {
                return _baseOffset;
            }
        }
    }

    public long TailOffset
    {
        get
        {
            lock (_lock)
            {
                return _baseOffset + _entries.Count;
            }
        }
    }

    public long Append(InMemoryEnvelope envelope)
    {
        TaskCompletionSource signalToRelease;
        long offset;

        lock (_lock)
        {
            offset = _baseOffset + _entries.Count;
            _entries.Add(envelope);
            _retainedBytes += Encoding.UTF8.GetByteCount(envelope.RawBody);
            TrimLocked();

            signalToRelease = _appendSignal;
            _appendSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signalToRelease.TrySetResult();
        return offset;
    }

    public long ResolveStartOffset(StartPosition startPosition)
    {
        lock (_lock)
        {
            return startPosition.Match(
                first: () => _baseOffset,
                last: () => _baseOffset + _entries.Count,
                offset: n => Math.Max(n, _baseOffset),
                timestamp: ResolveTimestampOffsetLocked);
        }
    }

    public IReadOnlyList<InMemoryEnvelope> ReadFrom(long offset, out long nextOffset)
    {
        lock (_lock)
        {
            return ReadFromLocked(offset, out nextOffset);
        }
    }

    /// <summary>
    ///     Reads entries from <paramref name="offset" /> onward, atomically capturing the append-notification signal to
    ///     await when nothing is available yet. Capturing the signal in the same locked section as the emptiness check
    ///     is required to avoid a lost-wakeup race: an append landing between a separate "is anything there" check and a
    ///     separate "subscribe to the next append" call would otherwise be missed, stalling the reader until some
    ///     unrelated future append happens to rescue it.
    /// </summary>
    public IReadOnlyList<InMemoryEnvelope> ReadFrom(long offset, out long nextOffset, out Task appendSignal)
    {
        lock (_lock)
        {
            var entries = ReadFromLocked(offset, out nextOffset);
            appendSignal = entries.Count == 0 ? _appendSignal.Task : Task.CompletedTask;
            return entries;
        }
    }

    private List<InMemoryEnvelope> ReadFromLocked(long offset, out long nextOffset)
    {
        var effectiveOffset = Math.Max(offset, _baseOffset);
        var startIndex = (int)(effectiveOffset - _baseOffset);

        if (startIndex >= _entries.Count)
        {
            nextOffset = effectiveOffset;
            return [];
        }

        nextOffset = _baseOffset + _entries.Count;
        return _entries.GetRange(startIndex, _entries.Count - startIndex);
    }

    public void TrimRetention()
    {
        lock (_lock)
        {
            TrimLocked();
        }
    }

    private long ResolveTimestampOffsetLocked(DateTime timestamp)
    {
        var target = ToDateTimeOffset(timestamp);

        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].EnqueuedTime >= target)
                return _baseOffset + i;
        }

        return _baseOffset + _entries.Count;
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime timestamp)
    {
        var utcTimestamp = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();

        return new DateTimeOffset(utcTimestamp);
    }

    private void TrimLocked()
    {
        if (retention.MaxAgeSeconds is { } maxAgeSeconds)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(maxAgeSeconds);

            while (_entries.Count > 0 && _entries[0].EnqueuedTime < cutoff)
                RemoveOldestLocked();
        }

        if (!referencePassingEnabled && retention.MaxLengthBytes is { } maxLengthBytes)
            while (_retainedBytes > maxLengthBytes && _entries.Count > 0)
                RemoveOldestLocked();
    }

    private void RemoveOldestLocked()
    {
        _retainedBytes -= Encoding.UTF8.GetByteCount(_entries[0].RawBody);
        _entries.RemoveAt(0);
        _baseOffset++;
    }
}
