using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Senders;

internal class MessageSender(IServiceProvider serviceProvider, IServiceBusClient serviceBusClient) : IMessageSender
{
    public async Task Send<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IMessageSubmitter<TMessage>>();
        await messageSubmitter.Submit(message, cancellationToken);
    }

    public async Task<JobId> SendJob<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IJobMessage
    {
        var messageSubmitter = serviceProvider.GetRequiredService<IJobSubmitter<TMessage>>();
        return await messageSubmitter.SubmitJob(message, cancellationToken);
    }

    public async Task SendMessage(string queueName, Guid messageId, string sessionId)
    {
        var sender = serviceBusClient.GetServiceBusSender(queueName);

        var serviceBusMessage = new ServiceBusMessage();
        serviceBusMessage.MessageId = messageId.ToString();
        serviceBusMessage.SessionId = sessionId;

        await sender.SendMessageAsync(serviceBusMessage);
    }

    public async Task StartRecurringJob(Guid jobId)
        => await Send(new StartRecurringJobMessage(jobId));
}