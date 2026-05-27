using Flowly;
using Receiver;
#if (UseDatabase && !HasDatabaseBackend)
#error Specify a database backend: --sqlserver, --postgres, or --sqlite
#endif

var builder = Host.CreateApplicationBuilder(args);

#if (UseRabbitMQ)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
#else
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
#endif

var host = builder.Build();

host.Run();
