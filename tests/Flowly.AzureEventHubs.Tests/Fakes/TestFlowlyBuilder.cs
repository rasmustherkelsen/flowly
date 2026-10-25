using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal static class TestFlowlyBuilder
{
    public static IFlowlyBuilder Create(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBusClientRegistry>(new MessageBusClientRegistry());
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(new MessagingTopologyCreatorRegistry());
        services.AddSingleton<IEventTopologyCreatorRegistry>(new EventTopologyCreatorRegistry());

        return new FlowlyBuilder(services, configuration ?? new ConfigurationBuilder().Build(), new KebabCaseTopologyNameResolver());
    }

    public static IFlowlyBuilder WithQueueProvider(this IFlowlyBuilder flowlyBuilder, string providerName = "queues")
    {
        var registry = ProviderNameResolver.GetRegistry(flowlyBuilder.Services);
        registry.Register(providerName, new FakeQueueMessageBusClient(), null);
        flowlyBuilder.Services.AddSingleton(new ProviderQueueManifest(providerName, registry.GetAll().Single(t => t.Name == providerName).IsPrimary, "Fake"));

        return flowlyBuilder;
    }

    public static IFlowlyBuilder WithEventHubs(this IFlowlyBuilder flowlyBuilder, FakeEventHubsClientFactory? clientFactory = null, string name = "azure-event-hubs", Azure.Storage.Blobs.BlobContainerClient? container = null, FakeEventHubsProcessorFactory? processorFactory = null)
        => flowlyBuilder.RegisterAzureEventHubs(
            clientFactory ?? new FakeEventHubsClientFactory(),
            processorFactory ?? new FakeEventHubsProcessorFactory(),
            name,
            EventHubsOwnershipOptions.Create(container, null, null),
            null);
}
