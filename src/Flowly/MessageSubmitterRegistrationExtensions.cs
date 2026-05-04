using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
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
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
            return flowlyBuilder;

        var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        flowlyBuilder.Services
            .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName, providerName))
            .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

        flowlyBuilder.Services.TryAddSingleton<IMessageSender, MessageSender>();

        return flowlyBuilder;
    }
}
