namespace Flowly;

/// <summary>
///     Opt-in, transport-agnostic, user-implemented storage for a <see cref="MessageStreamHandler{TMessage}" />'s
///     read position, restoring restart-survival that stream consumption does not have by default. Register an
///     implementation in DI (e.g. <c>services.AddSingleton&lt;MessageStreamCheckpoint&lt;TMessage&gt;, MyCheckpoint&gt;()</c>)
///     — Flowly detects and uses it automatically; no separate builder call is required.
///     <para>
///         Once registered, <see cref="MessageStreamHandlerOptions.StartPosition" /> becomes a bootstrap value used
///         only the first time this reader runs. Afterwards, the stored position takes over.
///     </para>
///     <para>
///         <strong>Not supported for InMemory-backed streams.</strong> The underlying in-process log has no
///         cross-restart persistence of its own — it is gone entirely on process restart — so persisting a position
///         into a durable store would point at data that no longer exists. Registering a checkpoint against an
///         InMemory-backed stream throws <see cref="InvalidOperationException" /> at registration time.
///     </para>
///     <para>
///         <strong>Run at most one live instance</strong> of a given stream handler registration against a shared
///         checkpoint store at a time. Flowly does not coordinate exclusive access across processes — running more
///         than one instance concurrently will corrupt the stored position.
///     </para>
/// </summary>
/// <typeparam name="TMessage">The message contract type this checkpoint tracks a position for.</typeparam>
public abstract class MessageStreamCheckpoint<TMessage>
{
    /// <summary>
    ///     Called once before this reader's processing loop starts, so an implementation can ensure a row exists
    ///     ahead of time — keeping <see cref="SaveStreamPosition" />, called on every processed batch, a plain
    ///     update with no existence check on the hot path.
    /// </summary>
    /// <param name="context">Identifies the reader this checkpoint belongs to.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    protected internal abstract Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the currently stored position for <paramref name="context" />, or <see langword="null" /> when
    ///     this reader has never successfully processed a batch — in which case Flowly falls back to the
    ///     <see cref="MessageStreamHandlerOptions.StartPosition" /> configured in
    ///     <see cref="MessageStreamHandler{TMessage}.Configure" />.
    /// </summary>
    /// <param name="context">Identifies the reader this checkpoint belongs to.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    /// <returns>The stored stream offset, or <see langword="null" /> when none has been saved yet.</returns>
    protected internal abstract Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Called after each batch is successfully processed, persisting the offset of the last message in the
    ///     batch. Never called for a batch that failed or is still retrying — a crash mid-batch replays at most the
    ///     last unsaved batch on restart.
    /// </summary>
    /// <param name="context">Identifies the reader this checkpoint belongs to and the position to store.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    protected internal abstract Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken);
}
