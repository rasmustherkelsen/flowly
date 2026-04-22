using Flowly;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[BatchProcessing(100, 30)]
internal class RebuildIndexBatchHandler(ILogger<RebuildIndexBatchHandler> logger) : BatchMessageHandler<RebuildIndexMessage>
{
    public override async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {
        logger.LogInformation("Received batch of {BatchSize} messages to rebuild index", messageContext.Messages.Count);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}