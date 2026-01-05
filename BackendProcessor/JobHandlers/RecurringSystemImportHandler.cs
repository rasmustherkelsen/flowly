using Flowly.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

public class RecurringSystemImportHandler(ILogger<RecurringSystemImportHandler> logger) : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling System Import Job at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
