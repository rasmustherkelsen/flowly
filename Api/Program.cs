using MessageContracts;
using Microsoft.AspNetCore.Mvc;
using SimpleTransit.MessageInfrastructure.Registration;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddAzureServiceBusClient(connectionName: "EmulatorNamespace");

builder.Services
    .AddRepositories(builder.Configuration.GetConnectionString("SqlServer")!)
    .AddJobHandlerStateDatabaseMigrations()
    .AddJobSubmitter<PerformStitchingOperationMessage>(QueuesNames.PerformStitching)
    .AddMessageSubmitter<RebuildIndexMessage>(QueuesNames.RebuildIndex);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/rebuild-index", async ([FromQuery] int? messageCount, IMessageSender messageSender) =>
{
    for (int i = 0; i < (messageCount ?? 1); i++)
    {
        await messageSender.Send(new RebuildIndexMessage(DateTime.UtcNow));
    }

    return Results.Ok("Ok");

}).WithName("RebuildIndex");

app.MapGet("/perform-stitching", async ([FromQuery] Guid importDefinitionId, [FromQuery] int? messageCount, [FromServices] IMessageSender messageSender) =>
{
    for (int i = 0; i < (messageCount ?? 1); i++)
    {
        await messageSender.SendJob(new PerformStitchingOperationMessage(importDefinitionId, $"My Stitching Operation {DateTime.UtcNow}"));
    }

    return Results.Ok("Ok");

}).WithName("PerformStitching");

app.MapGet("/run-recurring-job", async ([FromQuery] Guid jobId, [FromServices] IMessageSender messageSender) =>
{
    await messageSender.StartRecurringJob(jobId);
}).WithName("Run Recurring Job");

app.Run();
