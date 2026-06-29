using Flowly;
using JobTracker;
#if (UseRabbitMQ)
using Flowly.MessageInfrastructure;
#endif

var builder = Host.CreateApplicationBuilder(args);

#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); });
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var host = builder.Build();

host.Run();
