using Flowly;
using MyAspireApp.Receiver;
#if (UseRabbitMQ)
using Flowly.MessageInfrastructure;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); });
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
