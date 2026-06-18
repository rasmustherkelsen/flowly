using Flowly;
using App;
using App.Services;
#if (UseDashboard)
using Flowly.Dashboard;
#endif

#if (UseDashboard)
var builder = WebApplication.CreateBuilder(args);
#else
var builder = Host.CreateApplicationBuilder(args);
#endif

#if (UseDashboard)
builder.Services.AddFlowlyDashboard();
#endif
builder.AddFlowly<FlowlyConfiguration>();

builder.Services.AddHostedService<MessageSenderService>();
#if (UseJobTracking)
builder.Services.AddHostedService<JobSubmitterService>();
#endif
#if (UseDeadLetterTracking)
builder.Services.AddHostedService<FailingMessageSenderService>();
#endif

#if (UseDashboard)
var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
#else
var host = builder.Build();

host.Run();
#endif
