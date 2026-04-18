using Flowly.MessageInfrastructure.Events;
using MessageContracts;

namespace BackendProcessor.EventHandlers;

public class OrderProcessedEventHandler(ILogger<OrderProcessedEventHandler> logger) : EventHandlerBase<OrderProcessedEvent>
{
    public override Task Handle(IEventContext<OrderProcessedEvent> eventContext, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Sending e-mail to customer regarding order {eventContext.Event.OrderId} being processed");
        return Task.CompletedTask;
    }
}
