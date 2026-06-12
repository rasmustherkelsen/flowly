using Flowly;
using MyAspireApp.Receiver;
#if (UseDashboard)
using Flowly.Dashboard;
#endif
#if (UseDatabase && !HasDatabaseBackend)
#error Specify a database backend: --sqlserver, --postgres, or --sqlite
#endif

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

#if (UseDashboard)
builder.Services.AddFlowlyDashboard();
#endif
#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var app = builder.Build();

app.MapDefaultEndpoints();
#if (UseDashboard)
app.UseFlowlyDashboard();
#endif

app.Run();
