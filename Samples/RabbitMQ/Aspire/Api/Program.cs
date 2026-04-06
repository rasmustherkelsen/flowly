using FastEndpoints;
using FastEndpoints.Swagger;
using Flowly.MessageInfrastructure.Registration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();
builder.AddFlowly(options => options.CreateTopology = true);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
    app.UseSwaggerGen();

app.Run();
