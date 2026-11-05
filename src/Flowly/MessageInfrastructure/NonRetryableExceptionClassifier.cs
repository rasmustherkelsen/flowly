namespace Flowly.MessageInfrastructure;

/// <summary>
///     Decides whether a handling failure is permanent and must skip the retry policy. A failure is non-retryable when,
///     after unwrapping any <see cref="IHandlerExceptionWrapper" />, it is a <see cref="MessageRejectedException" /> or
///     is assignable to a type registered with <see cref="FlowlyOptions.NonRetryable{TException}" />.
/// </summary>
internal sealed class NonRetryableExceptionClassifier(FlowlyOptions flowlyOptions)
{
    private readonly Type[] _nonRetryableTypes = [.. flowlyOptions.NonRetryableExceptionTypes];

    /// <summary>
    ///     Determines whether <paramref name="exception" /> must not be retried.
    /// </summary>
    /// <param name="exception">The exception thrown while handling a message.</param>
    /// <returns><see langword="true" /> when the failure is permanent; otherwise <see langword="false" />.</returns>
    public bool IsNonRetryable(Exception exception)
    {
        var unwrapped = Unwrap(exception);
        if (unwrapped is MessageRejectedException)
        {
            return true;
        }

        foreach (var nonRetryableType in _nonRetryableTypes)
        {
            if (nonRetryableType.IsInstanceOfType(unwrapped))
            {
                return true;
            }
        }

        return false;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is IHandlerExceptionWrapper wrapper)
        {
            exception = wrapper.WrappedException;
        }

        return exception;
    }
}
