using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.BackgroundServices;

internal class JobHandlerBackgroundService<TMessage> : ServiceBusMessageHandlerBackgroundServiceBase<TMessage> where TMessage : class, IJobMessage
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public JobHandlerBackgroundService(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory,
        HandlerSettings<TMessage> handlerSettings,
        ILogger<JobHandlerBackgroundService<TMessage>> logger) : base(messageBusClient, serviceScopeFactory, handlerSettings, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task OnHandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobId = new JobId(Guid.Parse(receivedMessage.Properties.MessageId));
        var jobHandlerBase = serviceProvider.GetRequiredService<JobMessageHandlerBase<TMessage>>();
        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow, receivedMessage.Properties.RetryCount), cancellationToken);

        using var aliveSignalLinkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var aliveSignalTask = SendAliveSignalAsync(jobId, aliveSignalLinkedCancellationToken.Token);

        try
        {
            var context = new JobMessageContext<TMessage>(
                jobId,
                receivedMessage.Body,
                serviceProvider.GetRequiredService<IMessageSender>(),
                cancellationToken);

            await jobHandlerBase.Handle(context);
        }
        catch (Exception ex)
        {
            await aliveSignalLinkedCancellationToken.CancelAsync();
            await aliveSignalTask;
            throw new JobException(jobId, ex);
        }

        await aliveSignalLinkedCancellationToken.CancelAsync();
        await aliveSignalTask;

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow), cancellationToken);
    }

    protected override async Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobId = exception is JobException je ? je.JobId : new JobId(Guid.Parse(receivedMessage.Properties.MessageId));
        var reason = exception is JobException je2 ? (je2.InnerException?.Message ?? je2.Message) : exception.Message;

        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();
        await messageSender.Send(new JobFailed(jobId, reason, DateTime.UtcNow), cancellationToken);
        await receivedMessage.Complete(cancellationToken);
    }

    protected override Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception, "Job handler infrastructure error on queue");
        return Task.CompletedTask;
    }

    private async Task SendAliveSignalAsync(JobId jobId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
                await messageSender.Send(new JobIsAlive(jobId, DateTime.UtcNow), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }
}
