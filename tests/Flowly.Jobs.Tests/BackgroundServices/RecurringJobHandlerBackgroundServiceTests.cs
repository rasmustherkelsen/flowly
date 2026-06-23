using System.Diagnostics;
using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class RecurringJobHandlerBackgroundServiceTests
{
    public class ExecuteAsync
    {
        [Fact]
        public async Task CreatesExecutionLaneProcessorWithRecurringJobsQueueAndSessionName()
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var service = BuildService(client, new FakeMessageSender());

            await service.ExecuteAsync(CancellationToken.None);

            Assert.Equal(JobQueuesNames.RecurringJobs, client.CapturedQueueName);
            Assert.Equal("test-session", client.CapturedSessionName);
        }

        [Fact]
        public async Task SendsCreateRecurringJobStateMessageWithCorrectParameters()
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var messageSender = new FakeMessageSender();
            var service = BuildService(client, messageSender);

            await service.ExecuteAsync(CancellationToken.None);

            var message = Assert.Single(messageSender.SentMessages.OfType<CreateRecurringJobState>());
            Assert.Equal("test-session", message.JobTypeName);
            Assert.Equal("Test job description", message.Description);
            Assert.Equal("* * * * *", message.CronExpression);
        }

        [Fact]
        public async Task StartsProcessingOnExecutionLaneProcessor()
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var service = BuildService(client, new FakeMessageSender());

            await service.ExecuteAsync(CancellationToken.None);

            Assert.True(processor.StartProcessingCalled);
        }

        private static TestableService BuildService(FakeMessageBusClientWithLaneProcessor client, FakeMessageSender messageSender)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMessageSender>(messageSender);
            var provider = services.BuildServiceProvider();
            var registry = new FakeMessageBusClientRegistry(client);
            var settings = MakeSettings();

            return new TestableService(
                registry,
                provider.GetRequiredService<IServiceScopeFactory>(),
                settings,
                NullLogger<RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>>.Instance,
                new FakeHandlerInstrumentation());
        }
    }

    public class ProcessMessage
    {
        [Fact]
        public async Task SendsUpdateJobStateStartedBeforeInvokingHandler()
        {
            var (service, processor, messageSender, _) = BuildServiceWithProcessor();
            await service.ExecuteAsync(CancellationToken.None);

            await processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid()));

            Assert.Contains(messageSender.SentMessages.OfType<UpdateJobState>(), m => m.JobState == JobState.Started);
        }

        [Fact]
        public async Task CallsRecurringJobHandler()
        {
            var (service, processor, _, handler) = BuildServiceWithProcessor();
            await service.ExecuteAsync(CancellationToken.None);

            await processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid()));

            Assert.True(handler.WasCalled);
        }

        [Fact]
        public async Task SendsUpdateJobStateCompletedAfterHandlerSucceeds()
        {
            var (service, processor, messageSender, _) = BuildServiceWithProcessor();
            await service.ExecuteAsync(CancellationToken.None);

            await processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid()));

            Assert.Contains(messageSender.SentMessages.OfType<UpdateJobState>(), m => m.JobState == JobState.Completed);
        }

        [Fact]
        public async Task WhenHandlerThrows_WrapsExceptionInJobException()
        {
            var (service, processor, _, handler) = BuildServiceWithProcessor();
            handler.ExceptionToThrow = new InvalidOperationException("handler failed");
            await service.ExecuteAsync(CancellationToken.None);

            var ex = await Assert.ThrowsAsync<JobException>(() => processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid())));

            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task WhenHandlerThrows_DoesNotSendCompletedMessage()
        {
            var (service, processor, messageSender, handler) = BuildServiceWithProcessor();
            handler.ExceptionToThrow = new InvalidOperationException("handler failed");
            await service.ExecuteAsync(CancellationToken.None);

            await Assert.ThrowsAsync<JobException>(() => processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid())));

            Assert.DoesNotContain(messageSender.SentMessages.OfType<UpdateJobState>(), m => m.JobState == JobState.Completed);
        }

        [Fact]
        public async Task RecordsReceivedAndSucceededInstrumentation()
        {
            var instrumentation = new FakeHandlerInstrumentation();
            var (service, processor, _, _) = BuildServiceWithProcessor(instrumentation);
            await service.ExecuteAsync(CancellationToken.None);

            await processor.SimulateMessage(MakeReceivedMessage(Guid.NewGuid()));

            Assert.Single(instrumentation.ReceivedCalls);
            Assert.Single(instrumentation.SucceededCalls);
        }

        private static (TestableService service, FakeExecutionLaneProcessor processor, FakeMessageSender messageSender, CapturingRecurringJobHandler handler)
            BuildServiceWithProcessor(FakeHandlerInstrumentation? instrumentation = null)
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var messageSender = new FakeMessageSender();
            var handler = new CapturingRecurringJobHandler();

            var services = new ServiceCollection();
            services.AddSingleton<IMessageSender>(messageSender);
            services.AddSingleton(handler);
            var provider = services.BuildServiceProvider();

            var service = new TestableService(
                new FakeMessageBusClientRegistry(client),
                provider.GetRequiredService<IServiceScopeFactory>(),
                MakeSettings(),
                NullLogger<RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>>.Instance,
                instrumentation ?? new FakeHandlerInstrumentation());

            return (service, processor, messageSender, handler);
        }

        private static IReceivedMessage MakeReceivedMessage(Guid jobId) =>
            new FakeReceivedMessage(new MessageProperties(jobId.ToString(), string.Empty));
    }

    public class HandleError
    {
        [Fact]
        public async Task WhenJobException_SendsJobFailedMessage()
        {
            var (service, processor, messageSender) = BuildServiceWithProcessor();
            await service.ExecuteAsync(CancellationToken.None);

            var jobId = new JobId(Guid.NewGuid());
            await processor.SimulateError(new ErrorDetails(new JobException(jobId, new Exception("cause")), "endpoint"));

            var message = Assert.Single(messageSender.SentMessages.OfType<JobFailed>());
            Assert.Equal(jobId, message.JobId);
        }

        [Fact]
        public async Task WhenJobException_RecordsFailedInstrumentation()
        {
            var instrumentation = new FakeHandlerInstrumentation();
            var (service, processor, _) = BuildServiceWithProcessor(instrumentation);
            await service.ExecuteAsync(CancellationToken.None);

            var jobId = new JobId(Guid.NewGuid());
            await processor.SimulateError(new ErrorDetails(new JobException(jobId, new Exception("cause")), "endpoint"));

            Assert.Single(instrumentation.FailedCalls);
        }

        [Fact]
        public async Task WhenNotJobException_DoesNotSendJobFailedMessage()
        {
            var (service, processor, messageSender) = BuildServiceWithProcessor();
            await service.ExecuteAsync(CancellationToken.None);

            await processor.SimulateError(new ErrorDetails(new Exception("transport error"), "endpoint"));

            Assert.Empty(messageSender.SentMessages.OfType<JobFailed>());
        }

        private static (TestableService service, FakeExecutionLaneProcessor processor, FakeMessageSender messageSender)
            BuildServiceWithProcessor(FakeHandlerInstrumentation? instrumentation = null)
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var messageSender = new FakeMessageSender();

            var services = new ServiceCollection();
            services.AddSingleton<IMessageSender>(messageSender);
            services.AddSingleton<CapturingRecurringJobHandler>();
            var provider = services.BuildServiceProvider();

            var service = new TestableService(
                new FakeMessageBusClientRegistry(client),
                provider.GetRequiredService<IServiceScopeFactory>(),
                MakeSettings(),
                NullLogger<RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>>.Instance,
                instrumentation ?? new FakeHandlerInstrumentation());

            return (service, processor, messageSender);
        }
    }

    public class StopAsync
    {
        [Fact]
        public async Task WhenProcessorWasStarted_StopsAndDisposesProcessor()
        {
            var (service, processor) = BuildService();
            await service.ExecuteAsync(CancellationToken.None);

            await service.StopAsync(CancellationToken.None);

            Assert.True(processor.StopProcessingCalled);
            Assert.True(processor.DisposedAsync);
        }

        [Fact]
        public async Task WhenProcessorWasNotStarted_DoesNotThrow()
        {
            var (service, processor) = BuildService();

            await service.StopAsync(CancellationToken.None);

            Assert.False(processor.StopProcessingCalled);
        }

        private static (TestableService service, FakeExecutionLaneProcessor processor) BuildService()
        {
            var processor = new FakeExecutionLaneProcessor();
            var client = new FakeMessageBusClientWithLaneProcessor(processor);
            var messageSender = new FakeMessageSender();

            var services = new ServiceCollection();
            services.AddSingleton<IMessageSender>(messageSender);
            var provider = services.BuildServiceProvider();

            var service = new TestableService(
                new FakeMessageBusClientRegistry(client),
                provider.GetRequiredService<IServiceScopeFactory>(),
                MakeSettings(),
                NullLogger<RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>>.Instance,
                new FakeHandlerInstrumentation());

            return (service, processor);
        }
    }

    private static RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>.RecurringJobSettings MakeSettings() =>
        new("Test job description", "test-session", "* * * * *");

    private sealed class TestableService(
        Flowly.MessageInfrastructure.Registration.IMessageBusClientRegistry clientRegistry,
        IServiceScopeFactory serviceScopeFactory,
        RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>.RecurringJobSettings settings,
        ILogger<RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>> logger,
        IHandlerInstrumentation handlerInstrumentation)
        : RecurringJobHandlerBackgroundService<CapturingRecurringJobHandler>(clientRegistry, serviceScopeFactory, settings, logger, handlerInstrumentation)
    {
        public new Task ExecuteAsync(CancellationToken cancellationToken) => base.ExecuteAsync(cancellationToken);
        public new Task StopAsync(CancellationToken cancellationToken) => base.StopAsync(cancellationToken);
    }

    private sealed class CapturingRecurringJobHandler : RecurringJobHandler
    {
        public bool WasCalled { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public override Task Handle(CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeExecutionLaneProcessor : IExecutionLaneProcessor
    {
        public bool StartProcessingCalled { get; private set; }
        public bool StopProcessingCalled { get; private set; }
        public bool DisposedAsync { get; private set; }

        public event Func<IReceivedMessage, CancellationToken, Task>? ProcessMessage;
        public event Func<ErrorDetails, Task>? ProcessError;

        public Task StartProcessing(CancellationToken cancellationToken = default)
        {
            StartProcessingCalled = true;
            return Task.CompletedTask;
        }

        public Task StopProcessing(CancellationToken cancellationToken = default)
        {
            StopProcessingCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposedAsync = true;
            return ValueTask.CompletedTask;
        }

        public Task SimulateMessage(IReceivedMessage message, CancellationToken cancellationToken = default) =>
            ProcessMessage?.Invoke(message, cancellationToken) ?? Task.CompletedTask;

        public Task SimulateError(ErrorDetails errorDetails) =>
            ProcessError?.Invoke(errorDetails) ?? Task.CompletedTask;
    }

    private sealed class FakeMessageBusClientWithLaneProcessor(FakeExecutionLaneProcessor processor) : IMessageBusClient
    {
        public string MessagingSystem => "fake";
        public string? CapturedQueueName { get; private set; }
        public string? CapturedSessionName { get; private set; }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            CapturedQueueName = queueName;
            CapturedSessionName = laneFilter;
            return Task.FromResult<IExecutionLaneProcessor>(processor);
        }
    }

    private sealed class FakeHandlerInstrumentation : IHandlerInstrumentation
    {
        public bool IsEnabled => false;
        public List<string> ReceivedCalls { get; } = [];
        public List<string> SucceededCalls { get; } = [];
        public List<string> FailedCalls { get; } = [];

        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, ActivityContext parentContext = default) => null;
        public Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<ActivityLink> links) => null;
        public void RecordReceived(string handlerName, string queueName, long count = 1) => ReceivedCalls.Add(handlerName);
        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) => SucceededCalls.Add(handlerName);
        public void RecordFailed(string handlerName, string queueName, long count = 1) => FailedCalls.Add(handlerName);
        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    private sealed class FakeReceivedMessage(MessageProperties properties) : IReceivedMessage
    {
        public MessageProperties Properties { get; } = properties;
        public TBody GetBody<TBody>() => throw new NotSupportedException();
    }
}
