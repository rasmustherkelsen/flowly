using Flowly;
using Receiver;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);

var host = builder.Build();

host.Run();
