using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using MessageContracts;
using Microsoft.AspNetCore.Mvc;
using Flowly.AzureServiceBus;
using Flowly.Jobs.Registration;
using Flowly.Jobs.Senders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddFlowly<ApiFlowlyConfiguration>(builder.Configuration, options => options.CreateTopology = false);

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

app.MapGet("/perform-stitching", async ([FromQuery] Guid importDefinitionId, [FromQuery] int? messageCount, [FromServices] IJobMessageSender messageSender) =>
{
    for (int i = 0; i < (messageCount ?? 1); i++)
    {
        await messageSender.QueueJob(new PerformStitchingOperationMessage(importDefinitionId, $"My Stitching Operation {DateTime.UtcNow}"));
    }

    return Results.Ok("Ok");
}).WithName("PerformStitching");

app.MapGet("/run-recurring-job", async ([FromQuery] Guid jobId, [FromServices] IJobMessageSender messageSender) => { await messageSender.StartRecurringJob(jobId); }).WithName("Run Recurring Job");

app.Run();

public class ApiFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("EmulatorNamespace")
            .AddJobSubmitter<PerformStitchingOperationMessage>(QueuesNames.PerformStitching)
            .AddMessageSubmitter<RebuildIndexMessage>(QueuesNames.RebuildIndex);
    }
}