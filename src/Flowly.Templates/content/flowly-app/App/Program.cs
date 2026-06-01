using Flowly;
using App;
using App.Services;
#if (UseDatabase && !HasDatabaseBackend)
#error Specify a database backend: --sqlserver, --postgres, or --sqlite
#endif

var builder = Host.CreateApplicationBuilder(args);

#if (UseCallHandler)
builder.AddFlowly<FlowlyConfiguration>(options => options.InstanceName = "app");
#else
builder.AddFlowly<FlowlyConfiguration>();
#endif

builder.Services.AddHostedService<MessageSenderService>();
#if (UseJobTracking)
builder.Services.AddHostedService<JobSubmitterService>();
#endif
#if (UseDeadLetterTracking)
builder.Services.AddHostedService<FailingMessageSenderService>();
#endif

var host = builder.Build();

host.Run();
