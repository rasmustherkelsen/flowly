using BackendFinanceProcessor.EventHandlers;
using Flowly;
using Flowly.RabbitMQ;
using MessageContracts;

namespace BackendFinanceProcessor;

public class BackendFinanceProcessorFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddEventHandler<OrderProcessedEvent, FinanceOrderProcessedEventHandler>();
    }
}