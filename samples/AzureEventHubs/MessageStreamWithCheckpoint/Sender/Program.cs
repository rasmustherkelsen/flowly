using Flowly;
using Flowly.AzureEventHubs;
using Messages;
using Sender.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly(flowly => flowly
    .UseAzureEventHubs("EventHubs")
    .AddMessageRecorder<SensorReadingMessage>());

builder.Services.AddHostedService<SensorReadingSenderService>();

builder.Build().Run();
