namespace Flowly.Transport;

/// <summary>
///     Represents details of an error that occurred during message processing, including the exception and the endpoint
///     where the error occurred.
/// </summary>
/// <param name="Exception"></param>
/// <param name="EndPoint"></param>
public record ErrorDetails(Exception Exception, string EndPoint);