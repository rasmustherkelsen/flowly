using SimpleTransit.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

public class RecurringMoreFrequentImportHandler : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        Console.WriteLine("Handling More Frequent Import Job");
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}