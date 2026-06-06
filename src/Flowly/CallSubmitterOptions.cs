namespace Flowly;

/// <summary>
///     Per-submitter configuration options for <c>AddCallSubmitter&lt;TMessage&gt;</c>. Settings provided here take
///     precedence over the global defaults in <see cref="FlowlyOptions" />.
/// </summary>
public class CallSubmitterOptions
{
    /// <summary>
    ///     How long <see cref="IMessageCaller.Call{TMessage,TReturn}" /> waits for a response before throwing
    ///     <see cref="OperationCanceledException" />. When <see langword="null" />, the global
    ///     <see cref="FlowlyOptions.MessageCallTimeout" /> is used.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
