using Flowly;
using Receiver;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);

var host = builder.Build();

host.Run();
