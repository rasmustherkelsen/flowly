namespace Flowly.Transport;

/// <summary>
///     Optional capability of a transport client that can translate a <see cref="StartPosition" /> into the concrete
///     stream offset consumption would begin at, without consuming any message. Implemented by a client in addition to
///     <see cref="IStreamCapableMessageBusClient" />; Flowly detects it with a type test on the registered client, so
///     transports that do not implement it keep working unchanged.
///     <para>
///         Flowly uses it when a <see cref="MessageStreamCheckpoint{TMessage}" /> is registered and holds no position yet for a
///         reader: it resolves <see cref="StartPosition.Last" /> and <see cref="StartPosition.Timestamp" /> — the kinds
///         whose meaning moves while the process is down — once, and stores the result through
///         <see cref="MessageStreamCheckpoint{TMessage}.SaveStreamPosition" />, so the first run's start position stays fixed
///         even if no batch is ever processed. Without a resolver (or when it returns <see langword="null" />) the position is
///         stored only after the first processed batch.
///     </para>
///     <para>
///         <strong>Never answer too high.</strong> The returned offset is where consumption will start. An offset below the
///         true start only causes already-existing messages to be delivered again; an offset above it silently loses messages
///         from an append-only log. An implementation that cannot determine the offset exactly must round down, and one that
///         cannot decide at all must return <see langword="null" />.
///     </para>
/// </summary>
public interface IStreamStartOffsetResolver
{
    /// <summary>
    ///     Resolves <paramref name="startPosition" /> to the first stream offset a consumer attached with it would receive.
    ///     Flowly asks only for <see cref="StartPosition.Last" /> and <see cref="StartPosition.Timestamp" />; an
    ///     implementation may answer the other kinds too.
    /// </summary>
    /// <param name="queueName">The stream queue name. For a partitioned stream this is the partitioned queue's name; the transport maps <paramref name="partition" /> onto its own per-partition stream.</param>
    /// <param name="partition">The partition to resolve, or <see langword="null" /> for a non-partitioned stream.</param>
    /// <param name="startPosition">The configured start position to resolve.</param>
    /// <param name="cancellationToken">A cancellation token signaling when the host is shutting down.</param>
    /// <returns>
    ///     The first offset consumption would deliver — <c>0</c> for an empty stream resolved with
    ///     <see cref="StartPosition.Last" /> — or <see langword="null" /> when the position cannot be resolved without
    ///     consuming (for example because the broker did not answer in time).
    /// </returns>
    Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken);
}
