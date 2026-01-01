using Flowly.MessageInfrastructure.Model;
using Flowly.MessagingAbstractions;

namespace Flowly.MessageInfrastructure.Senders;

internal class RecurringJobInvoker(IMessageBusClient messageBusClient) : IRecurringJobInvoker
{
    public async Task Submit(RecurringJob recurringJob)
    {
        var messageBusSender = messageBusClient.CreateMessageBusSender(QueuesNames.RecurringJobs);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), string.Empty, recurringJob.JobTypeName));
    }
}