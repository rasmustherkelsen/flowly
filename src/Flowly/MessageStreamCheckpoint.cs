namespace Flowly;

/// <summary>
///     Opt-in, transport-agnostic, user-implemented storage for a <see cref="MessageStreamHandler{TMessage}" />'s
///     read position, restoring restart-survival that stream consumption does not have by default. Register an
///     implementation in DI (e.g. <c>services.AddSingleton&lt;MessageStreamCheckpoint&lt;TMessage&gt;, MyCheckpoint&gt;()</c>)
///     — Flowly detects and uses it automatically; no separate builder call is required.
///     <para>
///         Once registered, <see cref="MessageStreamHandlerOptions.StartPosition" /> becomes a bootstrap value used
///         only the first time this reader runs. Afterwards, the stored position takes over. The stored position means
///         "resume at <c>position + 1</c>": it is the offset of the last processed message, so a reader that has processed
///         nothing yet stores the offset it will start at minus one, which is <c>-1</c> for a stream that is read from its
///         very beginning.
///     </para>
///     <para>
///         <strong>The first run's position is fixed when the reader starts, not when its first batch completes.</strong> When
///         <see cref="GetStreamPosition" /> returns <see langword="null" /> and the transport can resolve the configured
///         <see cref="StartPosition.Last" /> or <see cref="StartPosition.Timestamp" /> to a concrete offset without consuming
///         (see <see cref="Transport.IStreamStartOffsetResolver" />), Flowly stores that offset minus one through
///         <see cref="SaveStreamPosition" /> before consumption starts, flagged with
///         <see cref="MessageStreamCheckpointSaveContext.IsBootstrapSeed" />. Messages published while the process was down
///         are then delivered after the next restart even if no batch was ever processed. <see cref="StartPosition.First" />
///         and <see cref="StartPosition.Offset" /> do not change meaning between restarts and are not stored up front. If the
///         transport cannot resolve the position, or storing it fails, the configured start position is used and the position
///         is first stored after the first processed batch.
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
///     <para>
///         <strong>Implementations must be thread-safe.</strong> Flowly resolves this type from DI and expects a
///         single instance to be registered with <c>AddSingleton</c>, shared for the lifetime of the process. For a
///         non-partitioned stream this is never called concurrently — one reader, one sequential processing loop.
///         For a <see cref="StreamPartitionsAttribute">partitioned</see> stream, Flowly runs one independent
///         processing loop per partition currently owned by this process, and every loop calls into the same
///         checkpoint instance — <see cref="InitializeCheckpoint" />, <see cref="GetStreamPosition" />, and
///         <see cref="SaveStreamPosition" /> can all be invoked concurrently from different partitions' loops at the
///         same time. Flowly never calls a method twice concurrently for the same
///         (<see cref="MessageStreamCheckpointContext.ConsumerName" />, <see cref="MessageStreamCheckpointContext.Partition" />)
///         pair, but an implementation must tolerate concurrent calls across different pairs. Avoid holding a single
///         non-thread-safe resource (e.g. an injected EF Core <c>DbContext</c>, which is not safe for concurrent
///         use) as instance state — inject an <c>IDbContextFactory&lt;T&gt;</c> and create a short-lived context per
///         call instead.
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
    ///     nothing has been saved for this reader yet — neither a processed batch nor a bootstrap seed — in which case
    ///     Flowly resolves the <see cref="MessageStreamHandlerOptions.StartPosition" /> configured in
    ///     <see cref="MessageStreamHandler{TMessage}.Configure" /> and, where it can, stores it through
    ///     <see cref="SaveStreamPosition" />. Consumption resumes at the returned offset plus one.
    /// </summary>
    /// <param name="context">Identifies the reader this checkpoint belongs to.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    /// <returns>
    ///     The stored stream offset — which can be <c>-1</c> when the reader was seeded to start at offset <c>0</c> — or
    ///     <see langword="null" /> when none has been saved yet. An implementation must return a stored <c>-1</c> as
    ///     <c>-1</c>, not as <see langword="null" />.
    /// </returns>
    protected internal abstract Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Called after each batch is successfully processed, persisting the offset of the last message in the
    ///     batch. Never called for a batch that failed or is still retrying — a crash mid-batch replays at most the
    ///     last unsaved batch on restart.
    ///     <para>
    ///         It is also called once, before consumption starts, when Flowly fixes the start position of a reader that has
    ///         none stored (see <see cref="MessageStreamCheckpointSaveContext.IsBootstrapSeed" />). That call happens without any
    ///         processed batch, and its <see cref="MessageStreamCheckpointSaveContext.Position" /> can be <c>-1</c> — the
    ///         reader will start at offset <c>0</c> — so an implementation must accept negative values and must not treat
    ///         every call as proof that a batch was handled. A row for the reader may not exist yet: store the position
    ///         whether or not <see cref="InitializeCheckpoint" /> created one.
    ///     </para>
    /// </summary>
    /// <param name="context">Identifies the reader this checkpoint belongs to and the position to store.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    protected internal abstract Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken);
}
