using BackendProcessor;
using Flowly;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddFlowly<BackendProcessorFlowlyConfiguration>(options => options.CreateTopology = false);

var app = builder.Build();

app.Run();