using BackendFinanceProcessor.EventHandlers;
using Flowly;
using Flowly.AzureServiceBus;
using MessageContracts;

namespace BackendFinanceProcessor;

internal class BackendFinanceProcessorFlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddEventHandler<OrderProcessedEvent, FinanceOrderProcessedEventHandler>();
    }
}
