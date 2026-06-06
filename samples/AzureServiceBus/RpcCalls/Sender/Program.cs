using Flowly;
using Sender;
using Sender.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;
    options.InstanceName = "sender";
});

builder.Services.AddHostedService<MessageSenderService>();

var app = builder.Build();

app.Run();
