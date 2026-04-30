using BackendFinanceProcessor.EventHandlers;
using Flowly;
using Flowly.AzureServiceBus;
using MessageContracts;

namespace BackendFinanceProcessor;

internal class BackendFinanceProcessorFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddEventHandler<OrderProcessedEvent, FinanceOrderProcessedEventHandler>();
    }
}
