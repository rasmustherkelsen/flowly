using BackendFinanceProcessor;
using Flowly;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<BackendFinanceProcessorFlowlyConfiguration>(options => options.CreateTopology = false);

var app = builder.Build();

app.Run();