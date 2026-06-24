using Flowly;
using Flowly.Dashboard;
using MyAspireApp.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseFlowlyDashboard();

app.Run();
