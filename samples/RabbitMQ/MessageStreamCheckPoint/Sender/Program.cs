using Flowly;
using Sender;
using Sender.Services;
using Flowly.MessageInfrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => { options.CreateTopology = true; options.WithTopologyNameResolver<DotCaseTopologyNameResolver>(); });

builder.Services.AddHostedService<MessageSenderService>();

var app = builder.Build();

app.Run();
