using Flowly.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

public class RecurringMoreFrequentImportHandler(ILogger<RecurringMoreFrequentImportHandler> logger) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling More Frequent Import Job");
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}