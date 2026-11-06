using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, initializeCheckpoint: true);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_DoesNotCallInitializeCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, initializeCheckpoint: false);

            Assert.Empty(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_StillReadsStoredPosition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, initializeCheckpoint: false);

            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task WhenInitializeCheckpointCompletes_InvokesCheckpointInitializedAfterIt()
        {
            var events = new List<string>();
            var checkpoint = new FakeCheckpoint(events);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                CreateScopeFactory(checkpoint), "consumer", 1, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, checkpointInitialized: () => events.Add("Initialized"));

            Assert.Equal(["Initialize", "Initialized", "Get"], events);
        }

        [Fact]
        public async Task WhenInitializeCheckpointThrows_DoesNotInvokeCheckpointInitialized()
        {
            var checkpoint = new FakeCheckpoint { InitializeException = new InvalidOperationException("store unavailable") };
            var initialized = false;

            await Assert.ThrowsAsync<InvalidOperationException>(() => MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                CreateScopeFactory(checkpoint), "consumer", 1, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, checkpointInitialized: () => initialized = true));

            Assert.False(initialized);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_DoesNotInvokeCheckpointInitialized()
        {
            var initialized = false;

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                CreateScopeFactory(new FakeCheckpoint()), "consumer", 1, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, initializeCheckpoint: false, checkpointInitialized: () => initialized = true);

            Assert.False(initialized);
        }

        [Fact]
        public async Task DefaultsToInitializingTheCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_ReturnsFallback()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
        }

        [Fact]
        public async Task WithStoredPositionAndLastFallback_ReturnsTheStoredPosition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance, CancellationToken.None);

            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task WithNoStoredPositionAndLastFallback_ReturnsLast()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance, CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
        }

        [Fact]
        public async Task WithStoredPosition_RecordsResumedOutcome()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 5 };
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", instrumentation, NullLogger.Instance, CancellationToken.None);

            var resolved = Assert.Single(instrumentation.CheckpointResolvedCalls);
            Assert.Equal(CheckpointResolution.Resumed, resolved.Resolution);
        }

        [Fact]
        public async Task WithNoStoredPosition_RecordsFreshOutcome()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), "MyHandler", "my-queue", instrumentation, NullLogger.Instance, CancellationToken.None);

            var resolved = Assert.Single(instrumentation.CheckpointResolvedCalls);
            Assert.Equal(CheckpointResolution.Fresh, resolved.Resolution);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_DoesNotRecordResolvedOutcome()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            var instrumentation = new SpyHandlerInstrumentation();

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", instrumentation, NullLogger.Instance, CancellationToken.None);

            Assert.Empty(instrumentation.CheckpointResolvedCalls);
        }

        [Fact]
        public async Task WithLastAndNoStoredPosition_SavesResolvedOffsetMinusOneAsBootstrapSeed()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120 };

            await Resolve(checkpoint, StartPosition.Last(), resolver, partition: 3);

            var save = Assert.Single(checkpoint.SaveCalls);
            Assert.Equal(119, save.Position);
            Assert.True(save.IsBootstrapSeed);
            Assert.Equal("consumer", save.ConsumerName);
            Assert.Equal(3, save.Partition);
        }

        [Fact]
        public async Task WithLastAndNoStoredPosition_ReturnsOffsetOfResolvedStart()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Offset(120), startPosition);
        }

        [Fact]
        public async Task WithResolvedStartOffsetZero_SavesMinusOne()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 0 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(-1, Assert.Single(checkpoint.SaveCalls).Position);
            Assert.Equal(StartPosition.Offset(0), startPosition);
        }

        [Fact]
        public async Task WithSeededMinusOne_NextResolutionResumesAtOffsetZero()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 0 };
            await Resolve(checkpoint, StartPosition.Last(), resolver);

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Offset(0), startPosition);
            Assert.Single(resolver.Calls);
        }

        [Fact]
        public async Task WithTimestampAndNoStoredPosition_SavesResolvedOffsetMinusOne()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 10 };

            var startPosition = await Resolve(checkpoint, StartPosition.Timestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), resolver);

            Assert.Equal(9, Assert.Single(checkpoint.SaveCalls).Position);
            Assert.Equal(StartPosition.Offset(10), startPosition);
        }

        [Fact]
        public async Task WithLastAndNoStoredPosition_AsksResolverForQueueAndPartitionAndConfiguredPosition()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 5 };

            await Resolve(checkpoint, StartPosition.Last(), resolver, partition: 2);

            var call = Assert.Single(resolver.Calls);
            Assert.Equal("my-queue", call.QueueName);
            Assert.Equal(2, call.Partition);
            Assert.Equal(StartPosition.Last(), call.StartPosition);
        }

        [Fact]
        public async Task WithFirstAndNoStoredPosition_DoesNotResolveOrSave()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 5 };

            var startPosition = await Resolve(checkpoint, StartPosition.First(), resolver);

            Assert.Empty(resolver.Calls);
            Assert.Empty(checkpoint.SaveCalls);
            Assert.Equal(StartPosition.First(), startPosition);
        }

        [Fact]
        public async Task WithOffsetAndNoStoredPosition_DoesNotResolveOrSave()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 5 };

            var startPosition = await Resolve(checkpoint, StartPosition.Offset(7), resolver);

            Assert.Empty(resolver.Calls);
            Assert.Empty(checkpoint.SaveCalls);
            Assert.Equal(StartPosition.Offset(7), startPosition);
        }

        [Fact]
        public async Task WithStoredPosition_DoesNotResolveOrSave()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var resolver = new FakeStreamStartOffsetResolver { Result = 5 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Empty(resolver.Calls);
            Assert.Empty(checkpoint.SaveCalls);
            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task WithNoResolver_ReturnsFallbackWithoutSavingAndRecordsFresh()
        {
            var checkpoint = new FakeCheckpoint();
            var instrumentation = new SpyHandlerInstrumentation();

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver: null, instrumentation: instrumentation);

            Assert.Equal(StartPosition.Last(), startPosition);
            Assert.Empty(checkpoint.SaveCalls);
            Assert.Equal(CheckpointResolution.Fresh, Assert.Single(instrumentation.CheckpointResolvedCalls).Resolution);
        }

        [Fact]
        public async Task WithResolverReturningNull_ReturnsFallbackWithoutSaving()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = null };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Last(), startPosition);
            Assert.Empty(checkpoint.SaveCalls);
        }

        [Fact]
        public async Task WithResolverReturningNull_LogsWarningAndRecordsSeedFailed()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = null };
            var instrumentation = new SpyHandlerInstrumentation();
            var logger = new CapturingLogger();

            await Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation, logger: logger);

            Assert.Equal(CheckpointResolution.SeedFailed, Assert.Single(instrumentation.CheckpointResolvedCalls).Resolution);
            Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        }

        [Fact]
        public async Task WithSeed_RecordsSeededOutcome()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 4 };
            var instrumentation = new SpyHandlerInstrumentation();

            await Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation);

            var resolved = Assert.Single(instrumentation.CheckpointResolvedCalls);
            Assert.Equal(CheckpointResolution.Seeded, resolved.Resolution);
            Assert.Equal("MyHandler", resolved.HandlerName);
            Assert.Equal("my-queue", resolved.QueueName);
        }

        [Fact]
        public async Task WithSeed_CallsInitializeGetResolveSaveInOrder()
        {
            var events = new List<string>();
            var checkpoint = new FakeCheckpoint(events);
            var resolver = new FakeStreamStartOffsetResolver(events) { Result = 4 };

            await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(["Initialize", "Get", "Resolve", "Get", "Save"], events);
        }

        [Fact]
        public async Task WhenAPositionIsStoredWhileTheStartOffsetIsResolved_DoesNotSaveTheSeed()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120, OnResolve = () => checkpoint.StoredPosition = 500 };

            await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Empty(checkpoint.SaveCalls);
            Assert.Equal(500, checkpoint.StoredPosition);
        }

        [Fact]
        public async Task WhenAPositionIsStoredWhileTheStartOffsetIsResolved_ResumesAfterTheStoredPosition()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120, OnResolve = () => checkpoint.StoredPosition = 500 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Offset(501), startPosition);
        }

        [Fact]
        public async Task WhenAPositionIsStoredWhileTheStartOffsetIsResolved_RecordsResumedOutcome()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120, OnResolve = () => checkpoint.StoredPosition = 500 };
            var instrumentation = new SpyHandlerInstrumentation();

            await Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation);

            Assert.Equal(CheckpointResolution.Resumed, Assert.Single(instrumentation.CheckpointResolvedCalls).Resolution);
        }

        [Fact]
        public async Task WhenAStoredMinusOneAppearsWhileTheStartOffsetIsResolved_ResumesAtOffsetZero()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120, OnResolve = () => checkpoint.StoredPosition = -1 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Offset(0), startPosition);
            Assert.Empty(checkpoint.SaveCalls);
        }

        [Fact]
        public async Task WhenNoPositionIsStoredWhileTheStartOffsetIsResolved_StillSavesTheSeed()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { Result = 120 };

            await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(119, Assert.Single(checkpoint.SaveCalls).Position);
        }

        [Fact]
        public async Task WithSeedAndInitializeCheckpointFalse_SeedsWithoutInitializing()
        {
            var events = new List<string>();
            var checkpoint = new FakeCheckpoint(events);
            var resolver = new FakeStreamStartOffsetResolver(events) { Result = 4 };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver, partition: 1, initializeCheckpoint: false);

            Assert.Equal(["Get", "Resolve", "Get", "Save"], events);
            Assert.Equal(StartPosition.Offset(4), startPosition);
        }

        [Fact]
        public async Task WhenResolverThrows_ReturnsFallbackWithoutSaving()
        {
            var checkpoint = new FakeCheckpoint();
            var resolver = new FakeStreamStartOffsetResolver { ExceptionToThrow = new InvalidOperationException("broker down") };

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver);

            Assert.Equal(StartPosition.Last(), startPosition);
            Assert.Empty(checkpoint.SaveCalls);
        }

        [Fact]
        public async Task WhenResolverThrows_LogsWarningWithExceptionAndRecordsSeedFailed()
        {
            var checkpoint = new FakeCheckpoint();
            var exception = new InvalidOperationException("broker down");
            var resolver = new FakeStreamStartOffsetResolver { ExceptionToThrow = exception };
            var instrumentation = new SpyHandlerInstrumentation();
            var logger = new CapturingLogger();

            await Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation, logger: logger);

            Assert.Equal(CheckpointResolution.SeedFailed, Assert.Single(instrumentation.CheckpointResolvedCalls).Resolution);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Same(exception, entry.Exception);
        }

        [Fact]
        public async Task WhenSaveThrows_ReturnsFallbackLogsWarningAndRecordsSeedFailed()
        {
            var checkpoint = new FakeCheckpoint { SaveException = new InvalidOperationException("store down") };
            var resolver = new FakeStreamStartOffsetResolver { Result = 4 };
            var instrumentation = new SpyHandlerInstrumentation();
            var logger = new CapturingLogger();

            var startPosition = await Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation, logger: logger);

            Assert.Equal(StartPosition.Last(), startPosition);
            Assert.Equal(CheckpointResolution.SeedFailed, Assert.Single(instrumentation.CheckpointResolvedCalls).Resolution);
            Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
        }

        [Fact]
        public async Task WhenSeedingIsCancelled_PropagatesTheCancellation()
        {
            var checkpoint = new FakeCheckpoint();
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var resolver = new FakeStreamStartOffsetResolver { ExceptionToThrow = new OperationCanceledException(cancellation.Token) };
            var instrumentation = new SpyHandlerInstrumentation();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                Resolve(checkpoint, StartPosition.Last(), resolver, instrumentation: instrumentation, cancellationToken: cancellation.Token));

            Assert.Empty(instrumentation.CheckpointResolvedCalls);
        }

        [Fact]
        public async Task WithNoCheckpointRegisteredAndResolver_DoesNotResolve()
        {
            var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            var resolver = new FakeStreamStartOffsetResolver { Result = 4 };

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), "MyHandler", "my-queue", new SpyHandlerInstrumentation(), NullLogger.Instance,
                CancellationToken.None, startOffsetResolver: resolver);

            Assert.Empty(resolver.Calls);
            Assert.Equal(StartPosition.Last(), startPosition);
        }

        private static Task<StartPosition> Resolve(
            FakeCheckpoint checkpoint,
            StartPosition fallback,
            IStreamStartOffsetResolver? resolver,
            int? partition = null,
            bool initializeCheckpoint = true,
            SpyHandlerInstrumentation? instrumentation = null,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
            => MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                CreateScopeFactory(checkpoint), "consumer", partition, fallback, "MyHandler", "my-queue", instrumentation ?? new SpyHandlerInstrumentation(),
                logger ?? NullLogger.Instance, cancellationToken, initializeCheckpoint, resolver);

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
        public async Task SavesBatchPositionWithoutBootstrapSeedFlag()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.SaveStreamPositionIfPresent<TestMessage>(
                scopeFactory, "consumer", 4, 7, "MyHandler", "my-queue", new SpyHandlerInstrumentation(), CancellationToken.None);

            var save = Assert.Single(checkpoint.SaveCalls);
            Assert.Equal(7, save.Position);
            Assert.Equal(4, save.Partition);
            Assert.False(save.IsBootstrapSeed);
        }

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

    private sealed class FakeCheckpoint(List<string>? events = null) : MessageStreamCheckpoint<TestMessage>
    {
        public long? StoredPosition { get; set; }
        public Exception? SaveException { get; set; }
        public Exception? InitializeException { get; set; }
        public List<MessageStreamCheckpointContext> InitializeCalls { get; } = [];
        public List<MessageStreamCheckpointSaveContext> SaveCalls { get; } = [];

        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        {
            events?.Add("Initialize");
            InitializeCalls.Add(context);
            return InitializeException is null ? Task.CompletedTask : Task.FromException(InitializeException);
        }

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        {
            events?.Add("Get");
            return Task.FromResult(StoredPosition);
        }

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
        {
            events?.Add("Save");

            if (SaveException is not null) return Task.FromException(SaveException);

            SaveCalls.Add(context);
            StoredPosition = context.Position;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, exception));
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
        public List<(string HandlerName, string QueueName, CheckpointResolution Resolution)> CheckpointResolvedCalls { get; } = [];
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

        public void RecordStreamPartitionTransition(string handlerName, string queueName, string consumerName, int? partition, StreamPartitionState state) { }

        public void RecordCheckpointSaved(string handlerName, string queueName, double durationMs) =>
            CheckpointSavedCalls.Add((handlerName, queueName, durationMs));

        public void RecordCheckpointSaveFailed(string handlerName, string queueName) =>
            CheckpointSaveFailedCalls.Add((handlerName, queueName));

        public void RecordCheckpointResolved(string handlerName, string queueName, CheckpointResolution resolution) =>
            CheckpointResolvedCalls.Add((handlerName, queueName, resolution));

        public System.Diagnostics.Activity? StartSendingResponse(string callQueueName, string replyQueueName, string messagingSystem, string messageId, string correlationId) => null;

        public void RecordResponseSent(string callQueueName, double durationMs) { }

        public void RecordResponseFailed(string callQueueName) { }
    }

    private record TestMessage;
}
