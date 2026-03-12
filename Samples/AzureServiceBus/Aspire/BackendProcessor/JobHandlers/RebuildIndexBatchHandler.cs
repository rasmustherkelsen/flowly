using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[QueueName(QueuesNames.RebuildIndex)]
[BatchProcessing(100, maxWaitTimeInSeconds: 30)]
public class RebuildIndexBatchHandler : BatchMessageHandlerBase<RebuildIndexMessage>
{
    public override async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {        
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}