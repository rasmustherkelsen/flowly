using Flowly;
using Receiver;
using Flowly.MessageInfrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); });

var host = builder.Build();

host.Run();
