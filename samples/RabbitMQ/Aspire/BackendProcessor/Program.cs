using BackendProcessor;
using Flowly;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFlowly<BackendProcessorFlowlyConfiguration>();

var app = builder.Build();
app.Run();
