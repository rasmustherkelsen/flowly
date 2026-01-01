using MessageContracts;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;

namespace BackendProcessor.JobHandlers;

public class RebuildIndexBatchHandler : IBatchMessageHandler<RebuildIndexMessage>
{
    public async Task Handle(IBatchMessageContext<RebuildIndexMessage> messageContext)
    {        
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}