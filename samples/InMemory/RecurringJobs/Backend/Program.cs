using Flowly;
using Flowly.InMemory;
using Flowly.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    null,
    flowlyBuilder =>
    {
        flowlyBuilder.UseInMemory();
        flowlyBuilder.AddSQLiteJobStateTracking("Data Source=flowly-jobs;Mode=Memory;Cache=Shared");
        flowlyBuilder.AddRecurringJob<HandleBatchOperation>();
    });

var app = builder.Build();

app.Run();

[RecurringJob("Do some batch operations", "*/10 * * * * *")] // Cron expression for every 15 seconds
internal class HandleBatchOperation(ILogger<HandleBatchOperation> logger) : RecurringJobHandler
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        logger.LogInformation("Batch operation executed at {DateTime}", DateTime.Now);
    }
}