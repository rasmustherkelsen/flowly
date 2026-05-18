using Flowly.Jobs;

namespace Api.Handlers.JobHandlers;

[RecurringJob("Import Frequent Data", "*/10 * * * * *")]
internal class FrequentlyRecurringHandler(ILogger<FrequentlyRecurringHandler> logger) : RecurringJobHandler
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling More Frequent Import Job at {Time}", DateTimeOffset.Now);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}
