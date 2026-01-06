using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.Jobs.Senders;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.BackgroundServices;

internal class ServiceBusJobHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class, IJobMessage
{
    public ServiceBusJobHandlerBackgroundService(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger<ServiceBusJobHandlerBackgroundService<TMessage>> logger) : base(messageBusClient, serviceScopeFactory, handlerSettings, logger)
    {
    }
    
    protected override async Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobId = Guid.Parse(receivedMessage.Properties.MessageId);

        var jobHandler = serviceProvider.GetRequiredService<IJobMessageHandler<TMessage>>();
        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow), cancellationToken);

        try
        {
            await jobHandler.Handle(new JobMessageContext<TMessage>(
                jobId,
                receivedMessage.Body,
                serviceProvider.GetRequiredService<IMessageSender>(),
                cancellationToken));
        }
        catch (Exception ex)
        {
            throw new JobException(jobId, ex);
        }

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow), cancellationToken);
    }

    protected override async Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        if (errorDetails.Exception is JobException jobException)
        {
            logger.LogError(errorDetails.Exception.Message);

            var messageSender = serviceProvider.GetRequiredService<IMessageSender>();
            await messageSender.Send(new JobFailed(jobException.JobId, jobException.InnerException?.Message ?? jobException.Message, DateTime.UtcNow));
        }
    }
}