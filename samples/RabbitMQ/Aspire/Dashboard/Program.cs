using Flowly;
using Flowly.Dashboard;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFlowlyDashboard();
builder.AddFlowly(options => options.CreateTopology = true);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseFlowlyDashboard();

app.Run();
