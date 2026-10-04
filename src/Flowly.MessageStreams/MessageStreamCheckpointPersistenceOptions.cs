namespace Flowly.MessageStreams;

/// <summary>
///     Options for a built-in message stream checkpoint persistence provider registered via
///     <c>Add&lt;Provider&gt;MessageStreamPersistence</c>.
/// </summary>
public class MessageStreamCheckpointPersistenceOptions
{
    /// <summary>
    ///     The number of times a transient database failure is retried before the operation fails. Defaults to
    ///     <c>5</c>, matching the retry policy used by Flowly's other EF Core-backed features (job state tracking,
    ///     dead letter tracking).
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>
    ///     The maximum delay between retry attempts for a transient database failure. Defaults to <c>30</c> seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}
