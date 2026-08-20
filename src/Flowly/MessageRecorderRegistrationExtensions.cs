using System.Reflection;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering message recorders in the Flowly framework. A message recorder
///     resolves the target stream queue name and provider for a message type and registers the internal infrastructure
///     needed to record messages via <see cref="IMessageRecorder" />.
/// </summary>
public static class MessageRecorderRegistrationExtensions
{
    /// <summary>
    ///     Registers a message recorder for TMessage, enabling <see cref="IMessageRecorder.Record{TMessage}" /> to
    ///     publish onto the message type's stream queue. The stream queue name is resolved by the active
    ///     <see cref="ITopologyNameResolver" /> (override with <see cref="QueueNameAttribute" />) and retention limits
    ///     are read from <see cref="StreamRetentionAttribute" /> on the message contract. Throws
    ///     <see cref="InvalidOperationException" /> at registration time when the resolved provider's client does not
    ///     implement <see cref="IStreamCapableMessageBusClient" /> (streams are currently supported on RabbitMQ and
    ///     InMemory only).
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register with.</param>
    /// <typeparam name="TMessage">The message contract type to record onto its stream.</typeparam>
    /// <returns>The <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the provider resolved for TMessage does not support message streaming.
    /// </exception>
    public static IFlowlyBuilder AddMessageRecorder<TMessage>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
            return flowlyBuilder;

        var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        ThrowIfNotStreamCapable(flowlyBuilder.Services, providerName);

        var partitionsAttribute = typeof(TMessage).GetCustomAttribute<StreamPartitionsAttribute>();
        if (partitionsAttribute != null &&
            ProviderNameResolver.GetRegistry(flowlyBuilder.Services).GetClient(providerName) is not IPartitionedStreamCapableMessageBusClient)
            throw new InvalidOperationException(
                $"{typeof(TMessage).Name} carries [StreamPartitions], but the message bus client for provider '{providerName}' does not " +
                $"support partitioned message streaming. The client must implement {nameof(IPartitionedStreamCapableMessageBusClient)}.");

        flowlyBuilder.Services
            .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName, providerName))
            .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

        flowlyBuilder.Services.TryAddSingleton<IMessageRecorder, MessageRecorder>();

        var retention = typeof(TMessage).GetCustomAttribute<StreamRetentionAttribute>();
        StreamQueueManifest.GetOrCreate(flowlyBuilder.Services)
            .MarkAsStream(queueName, retention?.MaxAgeSeconds, retention?.MaxLengthBytes, partitionsAttribute?.Count);

        FlowlySubmitterManifest.GetOrCreate(flowlyBuilder.Services)
            .Add(new DeferredSubmitterRegistration(typeof(TMessage), queueName, SubmitterKind.Stream));

        return flowlyBuilder.AddQueueRegistration(queueName, providerName: providerName);
    }

    internal static void ThrowIfNotStreamCapable(IServiceCollection services, string providerName)
    {
        if (ProviderNameResolver.GetRegistry(services).GetClient(providerName) is not IStreamCapableMessageBusClient)
            throw new InvalidOperationException(
                $"The message bus client for provider '{providerName}' does not support message streaming. " +
                $"The client must implement {nameof(IStreamCapableMessageBusClient)}.");
    }
}
