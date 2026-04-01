using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[BatchProcessing(maxMessagesBeforeProcessing: 100, maxWaitTimeInSeconds: 30)]
class RebuildIndexBatchHandler : BatchMessageHandlerBase<RebuildIndexMessage>
{
    public override async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
