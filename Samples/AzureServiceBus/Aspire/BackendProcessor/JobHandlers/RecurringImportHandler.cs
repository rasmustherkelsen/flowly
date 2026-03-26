using Flowly.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

[RecurringJob("Import System Data", "*/30 * * * * *")]
class RecurringImportHandler(ILogger<RecurringImportHandler> logger) : RecurringJobHandlerBase
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling System Import Job at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
