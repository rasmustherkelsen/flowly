using BackendFinanceProcessor.EventHandlers;
using Flowly;
using Flowly.OpenTelemetry;
using Flowly.RabbitMQ;
using MessageContracts;

namespace BackendFinanceProcessor;

internal class BackendFinanceProcessorFlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddEventHandler<OrderProcessedEvent, FinanceOrderProcessedEventHandler>();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}