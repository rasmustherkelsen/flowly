using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

public class OrderProcessor(ILogger<OrderProcessor> logger) : JobMessageHandlerBase<ProcessOrder>
{
    public override async Task Handle(IJobMessageContext<ProcessOrder> messageContext)
    {
        for (int i = 0; i < 10; i++)
        {
            logger.LogInformation("Processing order operation");
            await messageContext.SaveState(new { ProgressPercentage = (i + 1) * 10 });
            await Task.Delay(TimeSpan.FromSeconds(5), messageContext.CancellationToken);
        }
    }
}
