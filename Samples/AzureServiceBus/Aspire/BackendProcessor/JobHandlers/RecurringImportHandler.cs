using Flowly.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

class RecurringImportHandler(ILogger<RecurringImportHandler> logger) : RecurringJobHandlerBase
{
    public override void Configure(RecurringJobHandlerOptions options)
    {
        options.JobDescription = "The Recurring Import Job";
        options.CronExpression = "*/30 * * * * *";
    }

    public override async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling System Import Job at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
