using Api.Messages;
using Flowly;
using Flowly.Jobs;

namespace Api.Handlers.JobHandlers;

[MaxConcurrentCalls(5)]
internal class OrderProcessor(ILogger<OrderProcessor> logger, IServiceScopeFactory serviceScopeFactory) : JobHandler<ProcessOrder>
{
    public override async Task Handle(IJobMessageContext<ProcessOrder> messageContext)
    {
        var delay = TimeSpan.FromSeconds(Random.Shared.Next(1, 5));

        for (var i = 0; i < 10; i++)
        {
            logger.LogInformation("Processing order operation");
            await messageContext.SaveState(new { ProgressPercentage = (i + 1) * 10 });
            await Task.Delay(delay, messageContext.CancellationToken);
        }

        var scope = serviceScopeFactory.CreateScope();
        var eventSender = scope.ServiceProvider.GetRequiredService<IEventSender>();
        await eventSender.RaiseEvent(new OrderProcessedEvent(messageContext.Message.OrderId.ToString()), messageContext.CancellationToken);
    }
}
