using BackendFinanceProcessor.EventHandlers;
using Flowly;
using Flowly.AzureServiceBus;
using MessageContracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    configure =>
    {
        configure
            .UseAzureServiceBus("EmulatorNamespace")
            .AddEventHandler<OrderProcessedEvent, FinanceOrderProcessedEventHandler>();
    });

var app = builder.Build();

app.Run();