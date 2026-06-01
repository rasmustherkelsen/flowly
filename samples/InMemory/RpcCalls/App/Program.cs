using Flowly;
using App;
using App.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.InstanceName = "app");

builder.Services.AddHostedService<MessageSenderService>();

var host = builder.Build();

host.Run();
