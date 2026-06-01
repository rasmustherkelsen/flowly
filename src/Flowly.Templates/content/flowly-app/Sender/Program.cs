using Flowly;
using Sender;
using Sender.Services;

var builder = WebApplication.CreateBuilder(args);

#if (UseRabbitMQ && UseCallHandler)
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = true;
    options.InstanceName = "sender";
});
#elseif (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#elseif (UseCallHandler)
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;
    options.InstanceName = "sender";
});
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

app.Run();
