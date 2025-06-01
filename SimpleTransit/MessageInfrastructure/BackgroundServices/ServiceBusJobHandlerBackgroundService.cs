using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Receivers;
using SimpleTransit.MessageInfrastructure.Senders;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class ServiceBusJobHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class, IJobMessage
{
    public ServiceBusJobHandlerBackgroundService(
        IServiceBusClient client,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger<ServiceBusJobHandlerBackgroundService<TMessage>> logger) : base(client, serviceScopeFactory, handlerSettings, logger)
    {
    }

    protected internal override async Task OnHandleMessage(TMessage message, ProcessMessageEventArgs processMessageEventArgs, IServiceProvider serviceProvider)
    {
        var jobId = Guid.Parse(processMessageEventArgs.Message.MessageId);

        var jobHandler = serviceProvider.GetRequiredService<IJobMessageHandler<TMessage>>();
        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow));

        try
        {
            await jobHandler.Handle(new JobMessageContext<TMessage>(
                jobId,
                message,
                serviceProvider.GetRequiredService<IMessageSender>(),
                processMessageEventArgs.CancellationToken));
        }
        catch (Exception ex)
        {
            throw new JobException(jobId, ex);
        }

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow));
    }

    protected override async Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ProcessErrorEventArgs processMessageEventArgs)
    {
        if (processMessageEventArgs.Exception is JobException jobException)
        {
            logger.LogError(processMessageEventArgs.Exception.Message);

            var messageSender = serviceProvider.GetRequiredService<IMessageSender>();
            await messageSender.Send(new JobFailed(jobException.JobId, jobException.InnerException?.Message ?? jobException.Message, DateTime.UtcNow));
        }
    }
}