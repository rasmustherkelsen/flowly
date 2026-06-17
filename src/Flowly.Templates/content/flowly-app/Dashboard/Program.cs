using Flowly;
using Flowly.Dashboard;
using Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
