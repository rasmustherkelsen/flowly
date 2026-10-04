using Flowly.MessageInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class MessageStreamCheckpointRegistrationExtensionsTests
{
    public class AddMessageStreamCheckpoint
    {
        [Fact]
        public void WithValidCheckpointType_RegistersAgainstInferredMessageType()
        {
            var builder = new StubFlowlyBuilder();

            builder.AddMessageStreamCheckpoint<MyMessageCheckpoint>();

            using var provider = builder.Services.BuildServiceProvider();
            var checkpoint = provider.GetService<MessageStreamCheckpoint<MyMessage>>();
            Assert.IsType<MyMessageCheckpoint>(checkpoint);
        }

        [Fact]
        public void WithTypeNotInheritingMessageStreamCheckpoint_Throws()
        {
            var builder = new StubFlowlyBuilder();

            Assert.Throws<InvalidOperationException>(() => builder.AddMessageStreamCheckpoint<NotACheckpoint>());
        }
    }

    private sealed record MyMessage;

    private sealed class MyMessageCheckpoint : MessageStreamCheckpoint<MyMessage>
    {
        protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);

        protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NotACheckpoint;

    private sealed class StubFlowlyBuilder : IFlowlyBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }
}
