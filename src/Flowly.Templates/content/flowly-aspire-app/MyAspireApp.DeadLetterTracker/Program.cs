using Flowly;
using MyAspireApp.DeadLetterTracker;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
