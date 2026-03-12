using Flowly.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

[RecurringJob("Import Frequent Data", "*/10 * * * * *")]
public class RecurringMoreFrequentImportHandler(ILogger<RecurringMoreFrequentImportHandler> logger) : RecurringJobHandlerBase
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling More Frequent Import Job at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}