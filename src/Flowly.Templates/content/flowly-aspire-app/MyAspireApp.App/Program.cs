using Flowly;
using MyAspireApp.App;
using MyAspireApp.App.Services;
#if (UseDatabase && !HasDatabaseBackend)
#error Specify a database backend: --sqlserver, --postgres, or --sqlite
#endif

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

app.Run();
