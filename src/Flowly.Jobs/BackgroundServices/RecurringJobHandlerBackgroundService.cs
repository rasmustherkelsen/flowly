using System.Diagnostics;
using Cronos;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.BackgroundServices;

internal class RecurringJobHandlerBackgroundService<TRecurringJobHandler> : BackgroundService where TRecurringJobHandler : RecurringJobHandler
{
    private readonly IMessageBusClientRegistry _clientRegistry;
    private readonly IHandlerInstrumentation _handlerInstrumentation;
    private readonly string _handlerName = typeof(TRecurringJobHandler).Name;
    private readonly ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RecurringJobSettings _settings;
    private IExecutionLaneProcessor? _executionLaneProcessor;
    private string _messagingSystem = string.Empty;

    public RecurringJobHandlerBackgroundService(
        IMessageBusClientRegistry clientRegistry,
        IServiceScopeFactory serviceScopeFactory,
        RecurringJobSettings settings,
        ILogger<RecurringJobHandlerBackgroundService<TRecurringJobHandler>> logger,
        IHandlerInstrumentation handlerInstrumentation)
    {
        _clientRegistry = clientRegistry;
        _serviceScopeFactory = serviceScopeFactory;
        _settings = settings;
        _logger = logger;
        _handlerInstrumentation = handlerInstrumentation;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Recurring jobs always run on the primary provider
        var client = _clientRegistry.GetClient(_clientRegistry.PrimaryProviderName);
        _messagingSystem = client.MessagingSystem;

        _executionLaneProcessor = await client.CreateExecutionLaneProcessor(
            JobQueuesNames.RecurringJobs,
            _settings.SessionName,
            new MessageBusProcessorOptions(1, MessageBusReceiveMode.ReceiveAndDelete));

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        await messageSender.Send(new CreateRecurringJobState(_settings.SessionName, _settings.JobDescription, DateTime.UtcNow, _settings.CronExpression), stoppingToken);

        _executionLaneProcessor.ProcessMessage += OnProcessMessage;
        _executionLaneProcessor.ProcessError += OnHandleError;

        _logger.LogInformation("Recurring job '{RecurringJobHandlerName}' started. Cron expression: '{CronExpression}'", _handlerName, _settings.CronExpression);

        await _executionLaneProcessor.StartProcessing(stoppingToken);
    }

    private async Task OnProcessMessage(IReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running scheduled job '{RecurringJobHandlerName}'", _handlerName);

        _handlerInstrumentation.RecordReceived(_handlerName, JobQueuesNames.RecurringJobs);
        var sw = Stopwatch.StartNew();
        var parentContext = ActivityContextParser.Parse(receivedMessage.Properties);
        using var activity = _handlerInstrumentation.StartHandling(_handlerName, JobQueuesNames.RecurringJobs, _messagingSystem, receivedMessage.Properties, parentContext);

        var jobId = new JobId(Guid.Parse(receivedMessage.Properties.MessageId));

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

        _handlerInstrumentation.RecordSucceeded(_handlerName, JobQueuesNames.RecurringJobs, sw.Elapsed.TotalMilliseconds);
    }

    private async Task OnHandleError(ErrorDetails errorDetails)
    {
        if (errorDetails.Exception is JobException jobException)
        {
            _handlerInstrumentation.RecordFailed(_handlerName, JobQueuesNames.RecurringJobs);

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var messageSender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await messageSender.Send(new JobFailed(jobException.JobId, jobException.InnerException?.Message ?? jobException.Message, DateTime.UtcNow));
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executionLaneProcessor != null)
        {
            await _executionLaneProcessor.StopProcessing(cancellationToken);
            await _executionLaneProcessor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    public class RecurringJobSettings
    {
        public RecurringJobSettings(string jobDescription, string sessionName, string cronExpression)
        {
            JobDescription = jobDescription;
            SessionName = sessionName;
            CronExpression = cronExpression;

            if (!Cronos.CronExpression.TryParse(CronExpression, cronExpression.Split(' ').Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard, out _)) throw new ArgumentException("Invalid Cron expression", nameof(cronExpression));
        }

        public string JobDescription { get; }

        public string SessionName { get; }

        public string CronExpression { get; }
    }
}