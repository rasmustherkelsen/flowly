using BackendFinanceProcessor;
using Flowly.MessageInfrastructure.Registration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFlowly<BackendFinanceProcessorFlowlyConfiguration>();

var app = builder.Build();

app.Run();
