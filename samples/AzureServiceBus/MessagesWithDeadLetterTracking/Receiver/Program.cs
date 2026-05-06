using Flowly;
using Receiver;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(x => x.CreateTopology = false);

var app = builder.Build();

app.Run();
