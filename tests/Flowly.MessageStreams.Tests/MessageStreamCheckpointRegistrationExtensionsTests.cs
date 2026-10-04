using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageStreams.DatabaseModel;
using Flowly.MessageStreams.Tests.Fakes;
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
    {
        var builder = new StubFlowlyBuilder();
        var registry = new FakeMessageBusClientRegistry();
        registry.Register("primary", new FakeMessageBusClient(messagingSystem), null);
        builder.Services.AddSingleton<IMessageBusClientRegistry>(registry);
        return builder;
    }

    private sealed record TestMessage;
}
