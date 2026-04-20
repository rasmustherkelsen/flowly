namespace Flowly.MessageInfrastructure.RecurringJobs;

public abstract class RecurringJobHandlerBase : IRecurringJobHandler
{
    public virtual void Configure(RecurringJobHandlerOptions options)
    {
    }

    public abstract Task Handle(CancellationToken cancellationToken);
}
