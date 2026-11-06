namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Reported as the last error of a halted stream partition when the handler succeeded for a batch but acknowledging
///     the batch or saving its checkpoint kept failing, so an operator can tell it apart from a handler failure.
/// </summary>
internal sealed class StreamBatchAcknowledgementException(string message, Exception innerException) : Exception(message, innerException);
