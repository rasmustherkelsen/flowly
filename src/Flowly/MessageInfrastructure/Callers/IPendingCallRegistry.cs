namespace Flowly.MessageInfrastructure.Callers;

internal interface IPendingCallRegistry<TReturn> where TReturn : class
{
    /// <summary>
    ///     Registers a pending call keyed by <paramref name="correlationId" /> and returns a task that completes
    ///     when <see cref="TryResolve" /> is called with a matching ID, or is cancelled when
    ///     <paramref name="cancellationToken" /> fires.
    /// </summary>
    Task<TReturn> Register(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    ///     Resolves the pending call registered under <paramref name="correlationId" /> with the provided
    ///     <paramref name="response" />. Returns <see langword="true" /> when a matching pending call was found
    ///     and completed; <see langword="false" /> if the entry has already been removed (e.g. timed out).
    /// </summary>
    bool TryResolve(string correlationId, TReturn response);
}
