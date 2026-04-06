using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.Receivers;
using MessageContracts;

namespace BackendProcessor.JobHandlers;

[MaxConcurrentCalls(5)]
class OrderProcessor(ILogger<OrderProcessor> logger) : JobMessageHandlerBase<ProcessOrder>
{
    public override async Task Handle(IJobMessageContext<ProcessOrder> messageContext)
    {
        var delay = TimeSpan.FromSeconds(Random.Shared.Next(1, 5));

        for (int i = 0; i < 10; i++)
        {
            logger.LogInformation("Processing order operation");
            await messageContext.SaveState(new { ProgressPercentage = (i + 1) * 10 });
            await Task.Delay(delay, messageContext.CancellationToken);
        }
    }
}