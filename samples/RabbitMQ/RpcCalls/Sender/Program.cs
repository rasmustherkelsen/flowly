using Flowly;
using Sender;
using Sender.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = true;
    options.InstanceName = "sender";
});

builder.Services.AddHostedService<MessageCallerService>();

var app = builder.Build();

app.Run();
