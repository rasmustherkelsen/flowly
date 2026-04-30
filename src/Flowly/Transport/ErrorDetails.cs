namespace Flowly.Transport;

/// <summary>
///     Represents details of an error that occurred during message processing, including the exception and the endpoint
///     where the error occurred.
/// </summary>
/// <param name="Exception">The exception that caused the error.</param>
/// <param name="EndPoint">The queue, topic, or subscription name where the error occurred.</param>
public record ErrorDetails(Exception Exception, string EndPoint);