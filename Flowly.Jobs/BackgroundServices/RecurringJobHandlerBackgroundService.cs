using Cronos;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.RecurringJobs;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.BackgroundServices;

internal class RecurringJobHandlerBackgroundService<TRecurringJobHandler> : BackgroundService where TRecurringJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RecurringJobSettings _settings;
    private readonly ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> _logger;
    private readonly IExecutionLaneProcessor _executionLaneProcessor;

    public class RecurringJobSettings
    {
        public RecurringJobSettings(string jobDescription, string sessionName, string cronExpression)
        {
            JobDescription = jobDescription;
            SessionName = sessionName;
            CronExpression = cronExpression;

            if (!Cronos.CronExpression.TryParse(CronExpression, cronExpression.Split(' ').Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard, out _))
            {
                throw new ArgumentException("Invalid Cron expression", nameof(cronExpression));
            }
        }

        public string JobDescription { get; }
        public string SessionName { get; }
        public string CronExpression { get; }
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
        await messageSender.Send(new CreateRecurringJobState(_settings.SessionName, _settings.JobDescription, DateTime.UtcNow, _settings.CronExpression), stoppingToken);

        _executionLaneProcessor.ProcessMessage += OnProcessMessage;
        _executionLaneProcessor.ProcessError += OnHandleError;

        _logger.LogInformation($"Recurring job {typeof(TRecurringJobHandler).Name} started. Cron expression: {_settings.CronExpression}");

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