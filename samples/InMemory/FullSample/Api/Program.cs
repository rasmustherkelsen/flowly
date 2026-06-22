using Flowly;
using Flowly.Dashboard;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard();
builder.AddFlowly<ApiFlowlyConfiguration>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseFlowlyDashboard();

app.Run();
