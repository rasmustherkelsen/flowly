using FastEndpoints;
using FastEndpoints.Swagger;
using Flowly;
using Flowly.Dashboard;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();
builder.Services.AddFlowlyDashboard();
builder.AddFlowly<ApiFlowlyConfiguration>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseFlowlyDashboard();
app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
    app.UseSwaggerGen();

app.Run();
