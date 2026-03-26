using Flowly.Jobs.Model;
using Flowly.MessagingAbstractions;

namespace Flowly.Jobs.Senders;

internal class RecurringJobInvoker(IMessageBusClient messageBusClient) : IRecurringJobInvoker
{
    public async Task Submit(RecurringJob recurringJob)
    {
        var messageBusSender = messageBusClient.CreateMessageBusSender(JobQueuesNames.RecurringJobs);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), string.Empty, recurringJob.JobTypeName));
    }
}