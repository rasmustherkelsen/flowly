using Flowly;
using Flowly.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    flowlyBuilder => flowlyBuilder.UseRabbitMq(connection: "RabbitMQ", enableHealthCheck: true));

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
