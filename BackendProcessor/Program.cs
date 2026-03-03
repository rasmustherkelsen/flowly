using BackendProcessor;
using Flowly.MessageInfrastructure.Registration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddFlowly<BackendProcessorFlowlyConfiguration>(builder.Configuration, options => options.CreateTopology = false);

var app = builder.Build();

app.Run();