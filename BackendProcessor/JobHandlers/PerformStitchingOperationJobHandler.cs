using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[QueueName(QueuesNames.PerformStitching)]
public class PerformStitchingOperationJobHandler(ILogger<PerformStitchingOperationJobHandler> logger) : JobMessageHandlerBase<PerformStitchingOperationMessage>
{
    public override async Task Handle(IJobMessageContext<PerformStitchingOperationMessage> messageContext)
    {
        for (int i = 0; i < 10; i++)
        {
            logger.LogInformation("Running stitching operation");
            await messageContext.SaveState(new { ProgressPercentage = (i + 1) * 10 });
            await Task.Delay(TimeSpan.FromSeconds(5), messageContext.CancellationToken);
        }
    }
}
