using Flowly.Jobs.Senders;
using Flowly.Jobs.Tests.Fakes;
using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Tests.Registration;

public class SubmitterRegistrationExtensionsTests
{
    public class AddJobSubmitter
    {
        [Fact]
        public void WithQueueProvider_RegistersJobSubmitterForMessageType()
        {
            var builder = CreateBuilder(new FakeMessageBusClient());

            builder.AddJobSubmitter<SomeJobMessage>();

            Assert.Contains(builder.Services, s => s.ServiceType == typeof(IJobSubmitter<SomeJobMessage>));
        }

        [Fact]
        public async Task RegisteredJobSubmitterSendsToResolvedQueueNameOnResolvedProvider()
        {
            var client = new FakeMessageBusClient();
            var builder = CreateBuilder(client);
            builder.Services.AddSingleton<IMessageSender>(new FakeMessageSender());

            builder.AddJobSubmitter<SomeJobMessage>();

            using var serviceProvider = builder.Services.BuildServiceProvider();
            await serviceProvider.GetRequiredService<IJobSubmitter<SomeJobMessage>>().SubmitJob(new SomeJobMessage());
            Assert.Equal(["some-job"], client.CreatedSenders);
        }

        [Fact]
        public void WithStreamOnlyProvider_ThrowsDescriptiveError()
        {
            var builder = CreateBuilder(new FakeStreamOnlyClient());

            var exception = Assert.Throws<InvalidOperationException>(() => builder.AddJobSubmitter<SomeJobMessage>());

            Assert.Contains("requires a provider that supports queue-based messaging", exception.Message);
            Assert.Contains("primary (message streaming)", exception.Message);
        }
    }

    private static IFlowlyBuilder CreateBuilder(ITransportClient client)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register("primary", client, null);
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record SomeJobMessage : IJobMessage
    {
        public string Description => "some description";
        public string JobTypeName => "SomeJob";
    }
}
