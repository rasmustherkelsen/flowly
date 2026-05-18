using FastEndpoints;
using FastEndpoints.Swagger;
using Flowly;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();
builder.AddFlowly<ApiFlowlyConfiguration>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
    app.UseSwaggerGen();

app.Run();
