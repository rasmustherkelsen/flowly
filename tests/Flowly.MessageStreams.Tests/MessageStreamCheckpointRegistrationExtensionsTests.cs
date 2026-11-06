using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageStreams.DatabaseModel;
using Flowly.MessageStreams.Tests.Fakes;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams.Tests;

public class MessageStreamCheckpointRegistrationExtensionsTests
{
    public class UseMessageStreamCheckpoint
    {
        [Fact]
        public void AgainstInMemoryProvider_Throws()
        {
            var builder = CreateBuilder("in-memory");
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            Assert.Throws<InvalidOperationException>(() => builder.UseMessageStreamCheckpoint<TestMessage>());
        }

        [Fact]
        public void AgainstRabbitMqProvider_RegistersCheckpoint()
        {
            var builder = CreateBuilder("rabbitmq");
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            builder.UseMessageStreamCheckpoint<TestMessage>();

            using var provider = builder.Services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<MessageStreamCheckpoint<TestMessage>>());
        }

        [Fact]
        public void AgainstStreamOnlyProvider_RegistersCheckpoint()
        {
            var builder = CreateBuilder(new FakeStreamOnlyClient("event-hubs"));
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            builder.UseMessageStreamCheckpoint<TestMessage>();

            using var provider = builder.Services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<MessageStreamCheckpoint<TestMessage>>());
        }

        [Fact]
        public void WithQueueOnlyProviderRegisteredBeforeStreamOnlyProvider_ChecksTheStreamProvider()
        {
            var builder = new StubFlowlyBuilder();
            var registry = new FakeMessageBusClientRegistry();
            registry.Register("queues", new FakeQueueOnlyClient("in-memory"), null);
            registry.Register("event-hubs", new FakeStreamOnlyClient("event-hubs"), null);
            builder.Services.AddSingleton<IMessageBusClientRegistry>(registry);
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            builder.UseMessageStreamCheckpoint<TestMessage>();

            using var provider = builder.Services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<MessageStreamCheckpoint<TestMessage>>());
        }

        [Fact]
        public void WithoutStreamCapableProvider_ThrowsNamingContractAndCapability()
        {
            var builder = CreateBuilder(new FakeQueueOnlyClient("rabbitmq"));
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            var exception = Assert.Throws<InvalidOperationException>(() => builder.UseMessageStreamCheckpoint<TestMessage>());

            Assert.Contains(nameof(TestMessage), exception.Message);
            Assert.Contains("message streaming", exception.Message);
        }

        [Fact]
        public void AgainstStreamOnlyInMemoryProvider_Throws()
        {
            var builder = CreateBuilder(new FakeStreamOnlyClient("in-memory"));
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);

            Assert.Throws<InvalidOperationException>(() => builder.UseMessageStreamCheckpoint<TestMessage>());
        }

        [Fact]
        public void WithoutAnyPersistenceRegistered_Throws()
        {
            var builder = CreateBuilder("rabbitmq");

            Assert.Throws<InvalidOperationException>(() => builder.UseMessageStreamCheckpoint<TestMessage>());
        }

        [Fact]
        public void WithExplicitName_ResolvesThatStore()
        {
            var builder = CreateBuilder("rabbitmq");
            builder.AddMessageStreamCheckpointPersistence(null, options => options.UseSqlite("Data Source=:memory:"), false, null);
            builder.AddMessageStreamCheckpointPersistence("secondary", options => options.UseSqlite("Data Source=:memory:"), false, null);

            builder.UseMessageStreamCheckpoint<TestMessage>("secondary");

            using var provider = builder.Services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<MessageStreamCheckpoint<TestMessage>>());
        }

        [Fact]
        public void CalledBeforePersistenceIsRegistered_StillThrowsClearly()
        {
            var builder = CreateBuilder("rabbitmq");

            var exception = Assert.Throws<InvalidOperationException>(() => builder.UseMessageStreamCheckpoint<TestMessage>());

            Assert.Contains("Add<Provider>MessageStreamPersistence", exception.Message);
        }
    }

    private static StubFlowlyBuilder CreateBuilder(string messagingSystem)
        => CreateBuilder(new FakeQueueAndStreamClient(messagingSystem));

    private static StubFlowlyBuilder CreateBuilder(ITransportClient client)
    {
        var builder = new StubFlowlyBuilder();
        var registry = new FakeMessageBusClientRegistry();
        registry.Register("primary", client, null);
        builder.Services.AddSingleton<IMessageBusClientRegistry>(registry);
        return builder;
    }

    private sealed record TestMessage;
}
