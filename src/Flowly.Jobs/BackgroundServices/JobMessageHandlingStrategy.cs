using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Receivers;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.BackgroundServices;

internal class JobMessageHandlingStrategy<TMessage>(IServiceScopeFactory serviceScopeFactory) : IMessageHandlingStrategy<TMessage>
    where TMessage : class, IJobMessage
{
    public async Task HandleMessage(IReceivedMessage<TMessage> receivedMessage, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobId = new JobId(Guid.Parse(receivedMessage.Properties.MessageId));
        var jobHandlerBase = serviceProvider.GetRequiredService<JobMessageHandlerBase<TMessage>>();
        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow, receivedMessage.Properties.RetryCount), cancellationToken);

        using var aliveSignalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var aliveSignalTask = SendAliveSignal(jobId, aliveSignalCts.Token);

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
            await aliveSignalCts.CancelAsync();
            await aliveSignalTask;
            throw new JobException(jobId, ex);
        }

        await aliveSignalCts.CancelAsync();
        await aliveSignalTask;

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow), cancellationToken);
    }

    public async Task OnRetriesExhausted(IReceivedMessage<TMessage> receivedMessage, Exception exception, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobId = exception is JobException je ? je.JobId : new JobId(Guid.Parse(receivedMessage.Properties.MessageId));
        var reason = exception is JobException je2 ? je2.InnerException?.Message ?? je2.Message : exception.Message;

        var messageSender = serviceProvider.GetRequiredService<IMessageSender>();
        await messageSender.Send(new JobFailed(jobId, reason, DateTime.UtcNow), cancellationToken);
    }

    public Task OnMessageHandlingError(ILogger logger, IServiceProvider serviceProvider, ErrorDetails errorDetails)
    {
        logger.LogError(errorDetails.Exception, "Job handler infrastructure error on queue");
        return Task.CompletedTask;
    }

    private async Task SendAliveSignal(JobId jobId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
                await messageSender.Send(new JobIsAlive(jobId, DateTime.UtcNow), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}