using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleTransit.AzureServiceBusWrappers;
using SimpleTransit.MessageInfrastructure.Messages;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.RecurringJobs;
using SimpleTransit.MessageInfrastructure.Senders;

namespace SimpleTransit.MessageInfrastructure.BackgroundServices;

internal class RecurringJobHandlerBackgroundService<TRecurringJobHandler> : BackgroundService where TRecurringJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RecurringJobSettings _settings;
    private readonly ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> _logger;
    private readonly IServiceBusSessionProcessor _processor;

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
        IServiceBusClient client, 
        IServiceScopeFactory serviceScopeFactory, 
        RecurringJobSettings settings, 
        ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
        _logger = logger;

        var serviceBusSessionProcessorOptions = new ServiceBusSessionProcessorOptions
        {
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6)
        };

        serviceBusSessionProcessorOptions.SessionIds.Add(settings.SessionName);

        _processor = client.CreateSessionProcessor(QueuesNames.RecurringJobs, serviceBusSessionProcessorOptions);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        await messageSender.Send(new CreateRecurringJobState(_settings.SessionName, _settings.JobDescription, DateTime.UtcNow, _settings.Interval), stoppingToken);

        _processor.ProcessMessageAsync += HandleMessage;
        _processor.ProcessErrorAsync += HandleError;

        _logger.LogInformation($"Recurring job {typeof(TRecurringJobHandler).Name} started. Executing every {_settings.Interval.ToString()}");

        await _processor.StartProcessingAsync(stoppingToken);
    }

    protected internal async Task HandleMessage(ProcessSessionMessageEventArgs processSessionMessageEventArgs)
    {
        _logger.LogInformation($"Running scheduled job '{typeof(TRecurringJobHandler).Name}'");

        var jobId = Guid.Parse(processSessionMessageEventArgs.Message.MessageId);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await messageSender.Send(new UpdateJobState(jobId, JobState.Started, DateTime.UtcNow));

        try
        {
            var jobHandler = scope.ServiceProvider.GetRequiredService<TRecurringJobHandler>();
            await jobHandler.Handle(processSessionMessageEventArgs.CancellationToken);
        }
        catch (Exception ex)
        {
            throw new JobException(jobId, ex);
        }

        await messageSender.Send(new UpdateJobState(jobId, JobState.Completed, DateTime.UtcNow));
    }

    protected internal async Task HandleError(ProcessErrorEventArgs processErrorEventArgs)
    {
        if (processErrorEventArgs.Exception is JobException jobException)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await messageSender.Send(new JobFailed(jobException.JobId, jobException.InnerException?.Message ?? jobException.Message, DateTime.UtcNow));
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}