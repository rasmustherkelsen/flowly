using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[BatchProcessing(maxMessagesBeforeProcessing: 100, maxWaitTimeInSeconds: 30)]
class RebuildIndexBatchHandler(ILogger<RebuildIndexBatchHandler> logger) : BatchMessageHandlerBase<RebuildIndexMessage>
{
    public override async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {        
        logger.LogInformation("Received batch of {BatchSize} messages to rebuild index", messageContext.Messages.Count);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}