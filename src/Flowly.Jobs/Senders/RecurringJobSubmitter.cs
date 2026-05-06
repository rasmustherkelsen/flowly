using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Jobs.Senders;

internal class RecurringJobInvoker(IMessageBusClientRegistry clientRegistry) : IRecurringJobInvoker
{
    public async Task Submit(RecurringJob recurringJob)
    {
        var client = clientRegistry.GetClient(clientRegistry.PrimaryProviderName);
        var messageBusSender = await client.CreateMessageBusSender(JobQueuesNames.RecurringJobs);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), string.Empty, recurringJob.JobTypeName));
    }
}