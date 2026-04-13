using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Registration;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder.UseAzureServiceBus("AzureServiceBus", enableHealthCheck: true));

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
