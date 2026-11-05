namespace Flowly.MessageInfrastructure;

/// <summary>
///     Implemented by an exception that a message handling strategy uses to wrap the exception a handler threw, so
///     that <see cref="NonRetryableExceptionClassifier" /> can look through the wrapper to the original failure.
/// </summary>
internal interface IHandlerExceptionWrapper
{
    /// <summary>Gets the exception thrown by the handler that this exception wraps.</summary>
    Exception WrappedException { get; }
}
