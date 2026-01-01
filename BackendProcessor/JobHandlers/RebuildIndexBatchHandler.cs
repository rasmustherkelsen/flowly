using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

public class RebuildIndexBatchHandler : IBatchMessageHandler<RebuildIndexMessage>
{
    public async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {        
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}