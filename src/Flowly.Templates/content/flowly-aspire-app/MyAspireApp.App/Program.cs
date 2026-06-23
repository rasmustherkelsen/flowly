using Flowly;
using MyAspireApp.App;
using MyAspireApp.App.Services;
#if (UseDashboard)
using Flowly.Dashboard;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
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

var app = builder.Build();

app.MapDefaultEndpoints();
#if (UseDashboard)
app.UseFlowlyDashboard();
#endif

app.Run();
