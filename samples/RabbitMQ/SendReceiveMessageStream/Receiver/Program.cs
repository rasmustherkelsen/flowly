using Flowly;
using Flowly.MessageInfrastructure;
using Receiver;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.WithTopologyNameResolver<DotCaseTopologyNameResolver>());

var app = builder.Build();
app.Run();
