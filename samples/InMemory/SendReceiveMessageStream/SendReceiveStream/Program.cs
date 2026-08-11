using Flowly;
using SendReceiveStream;
using SendReceiveStream.Senders;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>();

builder.Services.AddHostedService<TelemetryMessageStreamSender>();

var app = builder.Build();
app.Run();
