using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering message submitters in the Flowly framework. A message submitter
///     resolves the target queue name and provider for a message type and registers the internal infrastructure needed
///     to send messages via <see cref="IMessageSender" />.
/// </summary>
public static class MessageSubmitterRegistrationExtensions
{
    /// <summary>
    ///     Registers a submitter for <typeparamref name="TMessage" /> so that messages of that type can be sent using
    ///     <see cref="IMessageSender.Send{TMessage}" />. The target queue name is resolved from the message contract
    ///     (via <see cref="QueueNameAttribute" /> or auto-generated kebab-case convention) and the provider is
    ///     resolved from <see cref="ProviderAffinityAttribute" /> or the single registered provider. Calling this
    ///     method more than once for the same type is safe — subsequent calls are ignored.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register the submitter with.</param>
    /// <typeparam name="TMessage">The type of message to register a submitter for.</typeparam>
    /// <returns>The same <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder AddMessageSubmitter<TMessage>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ServiceType == typeof(IMessageSubmitter<TMessage>)))
            return flowlyBuilder;

        var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
        var providerName = ProviderNameResolver.Resolve<IMessageBusClient>(flowlyBuilder.Services, typeof(TMessage));

        flowlyBuilder.Services
            .AddSingleton<IMessageSubmitter<TMessage>>(serviceProvider => new MessageSubmitter<TMessage>(
                serviceProvider.GetRequiredService<IMessageBusClientRegistry>(),
                queueName,
                providerName,
                serviceProvider.GetRequiredService<ISubmitterInstrumentation>()));

        flowlyBuilder.Services.TryAddSingleton<IMessageSender, MessageSender>();

        FlowlySubmitterManifest.GetOrCreate(flowlyBuilder.Services)
            .Add(new DeferredSubmitterRegistration(typeof(TMessage), queueName, SubmitterKind.Message));

        return flowlyBuilder;
    }
}
