using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

public class PerformStitchingOperationJobHandler : IJobMessageHandler<PerformStitchingOperationMessage>
{
    public async Task Handle(IJobMessageContext<PerformStitchingOperationMessage> messageContext)
    {
        for (int i = 0; i < 5; i++)
        {
            await messageContext.SaveState(new { ProgressPercentage = (i + 1) * 20 });
            await Task.Delay(TimeSpan.FromSeconds(5), messageContext.CancellationToken);
        }
    }
}