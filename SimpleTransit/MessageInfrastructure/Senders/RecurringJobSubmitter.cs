using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal class RecurringJobInvoker(IMessageSender messageSender) : IRecurringJobInvoker
{
    public async Task Submit(RecurringJob recurringJob) 
        => await messageSender.SendMessage(QueuesNames.RecurringJobs, recurringJob.JobId, recurringJob.JobTypeName);
}