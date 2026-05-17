using Api.Messages;
using Flowly;

namespace Api.Handlers.EventHandlers;

internal class OrderProcessedEventHandler(ILogger<OrderProcessedEventHandler> logger) : EventHandlerBase<OrderProcessedEvent>
{
    public override Task Handle(IEventContext<OrderProcessedEvent> eventContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Sending e-mail to customer regarding order {OrderId} being processed", eventContext.Event.OrderId);
        return Task.CompletedTask;
    }
}
