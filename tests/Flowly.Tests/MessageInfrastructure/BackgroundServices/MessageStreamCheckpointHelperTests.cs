using Flowly.MessageInfrastructure.BackgroundServices;
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
                scopeFactory, "consumer", null, StartPosition.First(), CancellationToken.None, initializeCheckpoint: true);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_DoesNotCallInitializeCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), CancellationToken.None, initializeCheckpoint: false);

            Assert.Empty(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithInitializeCheckpointFalse_StillReadsStoredPosition()
        {
            var checkpoint = new FakeCheckpoint { StoredPosition = 41 };
            var scopeFactory = CreateScopeFactory(checkpoint);

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", 2, StartPosition.First(), CancellationToken.None, initializeCheckpoint: false);

            Assert.Equal(StartPosition.Offset(42), startPosition);
        }

        [Fact]
        public async Task DefaultsToInitializingTheCheckpoint()
        {
            var checkpoint = new FakeCheckpoint();
            var scopeFactory = CreateScopeFactory(checkpoint);

            await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.First(), CancellationToken.None);

            Assert.Single(checkpoint.InitializeCalls);
        }

        [Fact]
        public async Task WithNoCheckpointRegistered_ReturnsFallback()
        {
            var services = new ServiceCollection();
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var startPosition = await MessageStreamCheckpointHelper.ResolveStartPosition<TestMessage>(
                scopeFactory, "consumer", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(StartPosition.Last(), startPosition);
        }

        private static IServiceScopeFactory CreateScopeFactory(FakeCheckpoint checkpoint)
        {
            var services = new ServiceCollection();
            services.AddSingleton<MessageStreamCheckpoint<TestMessage>>(checkpoint);
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

    private record TestMessage;
}
