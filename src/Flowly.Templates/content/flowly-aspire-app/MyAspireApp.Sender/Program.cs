using Flowly;
using MyAspireApp.Sender;
using MyAspireApp.Sender.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

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
