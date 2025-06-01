namespace SimpleTransit.MessageInfrastructure.RecurringJobs;

public interface IRecurringJobHandler
{
    Task Handle(CancellationToken cancellationToken);
}