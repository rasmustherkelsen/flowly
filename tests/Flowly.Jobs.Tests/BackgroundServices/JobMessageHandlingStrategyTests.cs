using Flowly.Jobs.BackgroundServices;
using Flowly.Jobs.Messages;
using Flowly.Jobs.Model;
using Flowly.Jobs.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.Jobs.Tests.BackgroundServices;

public class JobMessageHandlingStrategyTests
{
    public class HandleMessage
    {
        [Fact]
        public async Task SendsUpdateJobStateStartedAndCompletedAroundHandlerInvocation()
        {
            var jobId = Guid.NewGuid();
            var fakeMessageSender = new FakeMessageSender();
            var capturingJobHandler = new CapturingJobHandler();

            var serviceProvider = BuildServiceProvider(fakeMessageSender, capturingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(jobId.ToString(), string.Empty));

            await jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None);

            var updates = fakeMessageSender.SentMessages.OfType<FlowlysysUpdateJobStateMessage>().ToList();
            Assert.Equal(2, updates.Count);
            Assert.Equal(JobState.Started, updates[0].JobState);
            Assert.Equal(jobId, updates[0].JobId.InnerId);
            Assert.Equal(JobState.Completed, updates[1].JobState);
            Assert.Equal(jobId, updates[1].JobId.InnerId);
            Assert.True(capturingJobHandler.WasInvoked);
        }

        [Fact]
        public async Task PassesMessageBodyToHandler()
        {
            var fakeMessageSender = new FakeMessageSender();
            var capturingJobHandler = new CapturingJobHandler();

            var serviceProvider = BuildServiceProvider(fakeMessageSender, capturingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("payload-value"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None);

            Assert.NotNull(capturingJobHandler.ReceivedContext);
            Assert.Equal("payload-value", capturingJobHandler.ReceivedContext!.Message.Description);
        }

        [Fact]
        public async Task PassesRetryCountFromPropertiesToStartedUpdateJobState()
        {
            var fakeMessageSender = new FakeMessageSender();
            var capturingJobHandler = new CapturingJobHandler();

            var serviceProvider = BuildServiceProvider(fakeMessageSender, capturingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty, RetryCount: 4));

            await jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None);

            var startedUpdate = fakeMessageSender.SentMessages.OfType<FlowlysysUpdateJobStateMessage>().First();
            Assert.Equal(4, startedUpdate.RetryAttempt);
        }

        [Fact]
        public async Task WithThrowingHandler_WrapsExceptionInJobExceptionUsingMessageIdAsJobId()
        {
            var jobId = Guid.NewGuid();
            var fakeMessageSender = new FakeMessageSender();
            var throwingJobHandler = new ThrowingJobHandler(new InvalidOperationException("handler failed"));

            var serviceProvider = BuildServiceProvider(fakeMessageSender, throwingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(jobId.ToString(), string.Empty));

            var jobException = await Assert.ThrowsAsync<JobException>(
                () => jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None));

            Assert.Equal(jobId, jobException.JobId.InnerId);
            Assert.IsType<InvalidOperationException>(jobException.InnerException);
            Assert.Equal("handler failed", jobException.InnerException!.Message);
        }

        [Fact]
        public async Task WithThrowingHandler_DoesNotSendCompletedUpdate()
        {
            var fakeMessageSender = new FakeMessageSender();
            var throwingJobHandler = new ThrowingJobHandler(new InvalidOperationException("boom"));

            var serviceProvider = BuildServiceProvider(fakeMessageSender, throwingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await Assert.ThrowsAsync<JobException>(
                () => jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None));

            Assert.DoesNotContain(fakeMessageSender.SentMessages.OfType<FlowlysysUpdateJobStateMessage>(), u => u.JobState == JobState.Completed);
        }

        [Fact]
        public async Task DoesNotCallCompleteOnReceivedMessageOnSuccessPath()
        {
            var fakeMessageSender = new FakeMessageSender();
            var capturingJobHandler = new CapturingJobHandler();

            var serviceProvider = BuildServiceProvider(fakeMessageSender, capturingJobHandler);
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None);

            Assert.False(receivedMessage.WasCompleteCalled);
        }

        [Fact]
        public async Task WithNonGuidMessageId_ThrowsFormatException()
        {
            var serviceProvider = BuildServiceProvider(new FakeMessageSender(), new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties("not-a-guid", string.Empty));

            await Assert.ThrowsAsync<FormatException>(
                () => jobMessageHandlingStrategy.HandleMessage(receivedMessage, serviceProvider, CancellationToken.None));
        }
    }

    public class OnRetriesExhausted
    {
        [Fact]
        public async Task WithJobException_SendsJobFailedUsingJobExceptionJobIdAndInnerExceptionMessage()
        {
            var jobId = new JobId();
            var jobException = new JobException(jobId, new InvalidOperationException("inner reason"));
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await jobMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, jobException, serviceProvider, CancellationToken.None);

            var jobFailed = fakeMessageSender.SentMessages.OfType<FlowlysysJobFailedMessage>().Single();
            Assert.Equal(jobId, jobFailed.JobId);
            Assert.Equal("inner reason", jobFailed.FaultReason);
        }

        [Fact]
        public async Task WithJobExceptionWithoutInnerException_FallsBackToJobExceptionMessage()
        {
            var jobId = new JobId();
            var jobException = new JobException(jobId, null!);
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await jobMessageHandlingStrategy.OnRetriesExhausted(receivedMessage, jobException, serviceProvider, CancellationToken.None);

            var jobFailed = fakeMessageSender.SentMessages.OfType<FlowlysysJobFailedMessage>().Single();
            Assert.Null(jobException.InnerException);
            Assert.Equal(jobException.Message, jobFailed.FaultReason);
        }

        [Fact]
        public async Task WithNonJobException_UsesMessageIdAsJobIdAndExceptionMessage()
        {
            var jobId = Guid.NewGuid();
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(jobId.ToString(), string.Empty));

            await jobMessageHandlingStrategy.OnRetriesExhausted(
                receivedMessage,
                new InvalidOperationException("raw failure"),
                serviceProvider,
                CancellationToken.None);

            var jobFailed = fakeMessageSender.SentMessages.OfType<FlowlysysJobFailedMessage>().Single();
            Assert.Equal(jobId, jobFailed.JobId.InnerId);
            Assert.Equal("raw failure", jobFailed.FaultReason);
        }

        [Fact]
        public async Task CompletesReceivedMessage()
        {
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            await jobMessageHandlingStrategy.OnRetriesExhausted(
                receivedMessage,
                new InvalidOperationException("boom"),
                serviceProvider,
                CancellationToken.None);

            Assert.True(receivedMessage.WasCompleteCalled);
        }

        [Fact]
        public async Task PropagatesCancellationTokenToCompleteCall()
        {
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties(Guid.NewGuid().ToString(), string.Empty));

            using var cts = new CancellationTokenSource();

            await jobMessageHandlingStrategy.OnRetriesExhausted(
                receivedMessage,
                new InvalidOperationException("boom"),
                serviceProvider,
                cts.Token);

            Assert.Equal(cts.Token, receivedMessage.CompleteCancellationToken);
        }

        [Fact]
        public async Task WithNonJobExceptionAndNonGuidMessageId_ThrowsFormatException()
        {
            var fakeMessageSender = new FakeMessageSender();
            var serviceProvider = BuildServiceProvider(fakeMessageSender, new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var receivedMessage = new FakeReceivedMessage<TestJobMessage>(
                new TestJobMessage("import"),
                new MessageProperties("not-a-guid", string.Empty));

            await Assert.ThrowsAsync<FormatException>(() =>
                jobMessageHandlingStrategy.OnRetriesExhausted(
                    receivedMessage,
                    new InvalidOperationException("boom"),
                    serviceProvider,
                    CancellationToken.None));
        }
    }

    public class OnMessageHandlingError
    {
        [Fact]
        public async Task LogsErrorWithProvidedException()
        {
            var serviceProvider = BuildServiceProvider(new FakeMessageSender(), new CapturingJobHandler());
            var jobMessageHandlingStrategy = new JobMessageHandlingStrategy<TestJobMessage>(
                serviceProvider.GetRequiredService<IServiceScopeFactory>());

            var capturingLogger = new CapturingLogger();
            var exception = new InvalidOperationException("broker error");
            var errorDetails = new ErrorDetails(exception, "some-queue");

            await jobMessageHandlingStrategy.OnMessageHandlingError(capturingLogger, serviceProvider, errorDetails);

            var logEntry = Assert.Single(capturingLogger.Entries);
            Assert.Equal(LogLevel.Error, logEntry.Level);
            Assert.Equal(exception, logEntry.Exception);
        }
    }

    private static ServiceProvider BuildServiceProvider(IMessageSender messageSender, JobHandler<TestJobMessage> jobHandler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(messageSender);
        services.AddScoped<JobHandler<TestJobMessage>>(_ => jobHandler);

        return services.BuildServiceProvider();
    }

    private sealed record TestJobMessage(string Description) : IJobMessage
    {
        public string JobTypeName => nameof(TestJobMessage);
    }

    private sealed class CapturingJobHandler : JobHandler<TestJobMessage>
    {
        public bool WasInvoked { get; private set; }

        public IJobMessageContext<TestJobMessage>? ReceivedContext { get; private set; }

        public override Task Handle(IJobMessageContext<TestJobMessage> messageContext)
        {
            WasInvoked = true;
            ReceivedContext = messageContext;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingJobHandler(Exception exception) : JobHandler<TestJobMessage>
    {
        public override Task Handle(IJobMessageContext<TestJobMessage> messageContext) => throw exception;
    }

    private sealed class FakeReceivedMessage<TBody>(TBody body, MessageProperties properties) : IReceivedMessage<TBody>
    {
        public bool WasCompleteCalled { get; private set; }

        public CancellationToken CompleteCancellationToken { get; private set; }

        public TBody Body => body;

        public MessageProperties Properties => properties;

        public Task Complete(CancellationToken cancellationToken = default)
        {
            WasCompleteCalled = true;
            CompleteCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
