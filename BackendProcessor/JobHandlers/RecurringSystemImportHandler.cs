using SimpleTransit.MessageInfrastructure.RecurringJobs;

namespace BackendProcessor.JobHandlers;

public class RecurringSystemImportHandler : IRecurringJobHandler
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        Console.WriteLine("Handling System Import Job");
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
