namespace Flowly.DeadLetters;

/// <summary>
///     Thrown by <see cref="IDeadLetterService.Requeue(string, string?, string?, CancellationToken)"/> when the replacement message body is not acceptable:
///     it is empty or whitespace, larger than the maximum size, or not valid JSON although the stored body is. Nothing is published and the
///     dead letter stays <see cref="DeadLetterStatus.Pending"/>.
/// </summary>
public sealed class InvalidReplacementMessageBodyException : ArgumentException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InvalidReplacementMessageBodyException"/> class.
    /// </summary>
    /// <param name="message">A description of why the replacement message body was rejected.</param>
    public InvalidReplacementMessageBodyException(string message)
        : base(message)
    {
    }
}
