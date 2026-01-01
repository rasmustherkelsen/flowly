using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class RecurringJobHandlerBackgroundService<TRecurringJobHandler> : BackgroundService where TRecurringJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RecurringJobSettings _settings;
    private readonly ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> _logger;
    private readonly IExecutionLaneProcessor _executionLaneProcessor;

    public class RecurringJobSettings
    {
        public RecurringJobSettings(string jobDescription, string sessionName, TimeSpan interval)
        {
            JobDescription = jobDescription;
            SessionName = sessionName;
            Interval = interval;
        }

        public string JobDescription { get; }
        public string SessionName { get; }
        public TimeSpan Interval { get; }
    }

    public RecurringJobHandlerBackgroundService(
        IMessageBusClient messageBusClient,
        IServiceScopeFactory serviceScopeFactory, 
        RecurringJobSettings settings, 
        ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
        _logger = logger;

        _executionLaneProcessor = messageBusClient.CreateExecutionLaneProcessor(
            QueuesNames.RecurringJobs, 
            settings.SessionName, 
            new MessageBusProcessorOptions(1, MessageBusReceiveMode.ReceiveAndDelete));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        await messageSender.Send(new CreateRecurringJobState(_settings.SessionName, _settings.JobDescription, DateTime.UtcNow, _settings.Interval), stoppingToken);

        _executionLaneProcessor.ProcessMessage += OnProcessMessage;
        _executionLaneProcessor.ProcessError += OnHandleError;

        _logger.LogInformation($"Recurring job {typeof(TRecurringJobHandler).Name} started. Executing every {_settings.Interval.ToString()}");

        await _executionLaneProcessor.StartProcessing(stoppingToken);
    }

    private async Task OnProcessMessage(IReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running scheduled job '{RecurringJobHandlerName}'", typeof(TRecurringJobHandler).Name);

        var jobId = Guid.Parse(receivedMessage.Properties.MessageId);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow), cancellationToken);

        try
        {
            var jobHandler = scope.ServiceProvider.GetRequiredService<TRecurringJobHandler>();
            await jobHandler.Handle(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new JobException(jobId, ex);
        }

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow), cancellationToken);
    }
    
    private async Task OnHandleError(ErrorDetails errorDetails)
    {
        if (errorDetails.Exception is JobException jobException)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await messageSender.Send(new JobFailed(jobException.JobId, jobException.InnerException?.Message ?? jobException.Message, DateTime.UtcNow));
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _executionLaneProcessor.StopProcessing(cancellationToken);
        await _executionLaneProcessor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}