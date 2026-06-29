using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Jobs.Senders;

internal class RecurringJobInvoker(IMessageBusClientRegistry clientRegistry, ITopologyNameResolver topologyNameResolver) : IRecurringJobInvoker
{
    private readonly string _recurringJobsQueueName = topologyNameResolver.ResolveQueueName<FlowlysysRecurringJobsMessage>();

    public async Task Submit(RecurringJob recurringJob)
    {
        var client = clientRegistry.GetClient(clientRegistry.PrimaryProviderName);
        var messageBusSender = await client.CreateMessageBusSender(_recurringJobsQueueName);
        await messageBusSender.SendEmptyMessage(new MessageProperties(recurringJob.JobId.ToString(), string.Empty, recurringJob.JobTypeName));
    }
}