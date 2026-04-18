using BackendFinanceProcessor.EventHandlers;
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
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
