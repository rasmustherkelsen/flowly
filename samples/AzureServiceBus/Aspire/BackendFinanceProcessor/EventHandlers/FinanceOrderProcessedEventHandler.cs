using Flowly;
using MessageContracts;

namespace BackendFinanceProcessor.EventHandlers;

internal class FinanceOrderProcessedEventHandler(ILogger<FinanceOrderProcessedEventHandler> logger) : EventHandlerBase<OrderProcessedEvent>
{
    public override Task Handle(IEventContext<OrderProcessedEvent> eventContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Writing order {OrderId} to accounting system", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}