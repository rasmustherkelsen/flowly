using BackendFinanceProcessor;
using Flowly;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddFlowly<BackendFinanceProcessorFlowlyConfiguration>();

var app = builder.Build();

app.Run();
