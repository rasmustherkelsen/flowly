using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.BackgroundServices;

public class MessageStreamCheckpointHelperTests
{
    public class ResolveStartPosition
    {
        [Fact]
        public async Task WithInitializeCheckpointTrue_CallsInitializeCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(),
                CancellationToken.None, initializeCheckpoint: true);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_DoesNotCallInitializeCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(),
                CancellationToken.None, initializeCheckpoint: false);

            Assert.Empty(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_StillReadsStoredPosition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(),
                CancellationToken.None, initializeCheckpoint: false);

            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task DefaultsToInitializingTheCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(),
                CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_ReturnsFallback()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(),
                CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
        }

        [Fact]
        public async Task WithStoredPositionAndLastFallback_ReturnsTheStoredPosition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), CancellationToken.None);

            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task WithNoStoredPositionAndLastFallback_ReturnsLast()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
        }

        [Fact]
        public async Task WithStoredPosition_RecordsResumedOutcome()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 5 };
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", instrumentation, CancellationToken.None);

            var resolved = Assert.Single(instrumentation.CheckpointResolvedCalls);
            Assert.True(resolved.Resumed);
        }

        [Fact]
        public async Task WithNoStoredPosition_RecordsFreshOutcome()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", instrumentation, CancellationToken.None);

            var resolved = Assert.Single(instrumentation.CheckpointResolvedCalls);
            Assert.False(resolved.Resumed);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_DoesNotRecordResolvedOutcome()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", instrumentation, CancellationToken.None);

            Assert.Empty(instrumentation.CheckpointResolvedCalls);
        }

        private static IServiceScopeFactory CreateScopeFactory(FakeCheckpoint checkpoint)
        {
            var services = new ServiceCollection();
            services.AddSingleton<MessageStreamCheckpoint<TestMessage>>(checkpoint);
            return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        }
    }

    public class ThrowIfCheckpointRegisteredForInMemory
    {
        [Fact]
        public void WithCheckpointRegisteredAndInMemory_Throws()
        {
            var scopeFactory = CreateScopeFactory(registerCheckpoint: true);

            Assert.Throws<InvalidOperationException>(() =>
                MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TestMessage>(scopeFactory, "in-memory"));
        }

        [Fact]
        public void WithCheckpointRegisteredAndRabbitMq_DoesNotThrow()
        {
            var scopeFactory = CreateScopeFactory(registerCheckpoint: true);

            MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TestMessage>(scopeFactory, "rabbitmq");
        }

        [Fact]
        public void WithNoCheckpointRegisteredAndInMemory_DoesNotThrow()
        {
            var scopeFactory = CreateScopeFactory(registerCheckpoint: false);

            MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TestMessage>(scopeFactory, "in-memory");
        }

        [Fact]
        public void WithCheckpointRegisteredAsTypeAndInMemory_ThrowsWithoutConstructingIt()
        {
            var services = new ServiceCollection();
            services.AddSingleton<MessageStreamCheckpoint<TestMessage>, UnconstructableCheckpoint>();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                MessageStreamCheckpointHelper.ThrowIfCheckpointRegisteredForInMemory<TestMessage>(scopeFactory, "in-memory"));

            Assert.Contains("InMemory-backed streams", exception.Message);
        }

        private static IServiceScopeFactory CreateScopeFactory(bool registerCheckpoint)
        {
            var services = new ServiceCollection();
            if (registerCheckpoint) services.AddSingleton<MessageStreamCheckpoint<TestMessage>>(new FakeCheckpoint());
            return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        }
    }

    public class SaveStreamPositionIfPresent
    {
        [Fact]
        public async Task WhenSaveSucceeds_RecordsCheckpointSaved()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TestMessage>(
                scopeFactory, "consumer", null, 7, "MyHandler", "my-queue", instrumentation, CancellationToken.None);

            Assert.Single(instrumentation.CheckpointSavedCalls);
            Assert.Empty(instrumentation.CheckpointSaveFailedCalls);
        }

        [Fact]
        public async Task WhenSaveThrows_RecordsCheckpointSaveFailedAndRethrows()
        {
            var checkpoint = new ThrowingCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TestMessage>(
                    scopeFactory, "consumer", null, 7, "MyHandler", "my-queue", instrumentation, CancellationToken.None));

            Assert.Single(instrumentation.CheckpointSaveFailedCalls);
            Assert.Empty(instrumentation.CheckpointSavedCalls);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_DoesNotRecordAnything()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TestMessage>(
                scopeFactory, "consumer", null, 7, "MyHandler", "my-queue", instrumentation, CancellationToken.None);

            Assert.Empty(instrumentation.CheckpointSavedCalls);
            Assert.Empty(instrumentation.CheckpointSaveFailedCalls);
        }

        private static IServiceScopeFactory CreateScopeFactory(MessageStreamCheckpoint<TestMessage> checkpoint)
        {
            var services = new ServiceCollection();
            services.AddSingleton(checkpoint);
            return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        }
    }

    private sealed class FakeCheckpoint : MessageStreamCheckpoint<TestMessage>
    {
        public long? StoredPosition { get; set; }
        public List<MessageStreamCheckpointContext> InitializeCalls { get; } = [];

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        {
            InitializeCalls.Add(context);
            return Task.CompletedTask;
        }

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.FromResult(StoredPosition);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class UnconstructableCheckpoint : MessageStreamCheckpoint<TestMessage>
    {
        public UnconstructableCheckpoint() => throw new InvalidOperationException("must not be constructed");

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ThrowingCheckpoint : MessageStreamCheckpoint<TestMessage>
    {
        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("save failed");
    }

    private sealed class SpyHandlerInstrumentation : IHandlerInstrumentation
    {
        public List<(string HandlerName, string QueueName, bool Resumed)> CheckpointResolvedCalls { get; } = [];
        public List<(string HandlerName, string QueueName, double DurationMs)> CheckpointSavedCalls { get; } = [];
        public List<(string HandlerName, string QueueName)> CheckpointSaveFailedCalls { get; } = [];

        public bool IsEnabled => false;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, System.Diagnostics.ActivityContext parentContext = default)
            => null;

        public System.Diagnostics.Activity? StartHandling(string handlerName, string queueName, string messagingSystem, MessageProperties messageProperties, IEnumerable<System.Diagnostics.ActivityLink> links)
            => null;

        public void RecordReceived(string handlerName, string queueName, long count = 1) { }

        public void RecordSucceeded(string handlerName, string queueName, double durationMs, long count = 1) { }

        public void RecordFailed(string handlerName, string queueName, long count = 1) { }

        public void RecordRetried(string handlerName, string queueName, long count = 1) { }

        public void RecordHalted(string handlerName, string queueName, string reason) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) =>
            CheckpointSavedCalls.Add((handlerName, queueName, durationMs));

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) =>
            CheckpointSaveFailedCalls.Add((handlerName, queueName));

        public void RecordCheckpointResolved(string handlerName, string queueName, bool resumed) =>
            CheckpointResolvedCalls.Add((handlerName, queueName, resumed));

        public System.Diagnostics.Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    private record TestMessage;
}
