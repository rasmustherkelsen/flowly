namespace Flowly;

public class BatchMessageHandlerOptions
{
    public int? MaxMessagesBeforeProcessing { get; set; }

    public TimeSpan? MaxWaitTime { get; set; }
}