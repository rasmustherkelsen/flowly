using Flowly;
using Receiver;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<ReceiverFlowlyConfiguration>(x => x.CreateTopology = true);

var app = builder.Build();

app.Run();
