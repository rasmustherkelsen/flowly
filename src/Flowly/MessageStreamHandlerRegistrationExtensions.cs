using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering message stream handlers in the Flowly framework.
/// </summary>
public static class MessageStreamHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers a <see cref="MessageStreamHandler{TMessage}" /> consuming the stream queue resolved for TMessage.
    ///     The handler must set <see cref="MessageStreamHandlerOptions.StartPosition" /> in its
    ///     <see cref="MessageStreamHandler{TMessage}.Configure" /> override — registration throws when no start
    ///     position is configured. Retry is opt-in via <see cref="RetryPolicyAttribute" /> on the handler class and
    ///     runs in-process; when retries are exhausted the handler halts consumption entirely rather than skipping the
    ///     failed batch. Throws <see cref="InvalidOperationException" /> at registration time when the resolved
    ///     provider's client does not implement <see cref="IStreamCapableMessageBusClient" /> (streams are currently
    ///     supported on RabbitMQ and InMemory only).
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register with.</param>
    /// <typeparam name="TMessage">The message contract type consumed from the stream.</typeparam>
    /// <typeparam name="THandler">The handler type processing the stream.</typeparam>
    /// <returns>The <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the provider resolved for TMessage does not support message streaming, or when
    ///     THandler does not configure a start position.
    /// </exception>
    public static IFlowlyBuilder AddMessageStreamHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : MessageStreamHandler<TMessage>
        where TMessage : class
    {
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

        MessageRecorderRegistrationExtensions.ThrowIfNotStreamCapable(flowlyBuilder.Services, providerName);

        var resolved = MessageStreamHandlerOptionsResolver.Resolve<THandler, TMessage>(flowlyBuilder.TopologyNameResolver);

        StreamQueueManifest.GetOrCreate(flowlyBuilder.Services)
            .MarkAsStream(resolved.QueueName, resolved.MaxAgeSeconds, resolved.MaxLengthBytes);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddSingleton(new MessageStreamHandlerSettings<TMessage, THandler>(
                resolved.QueueName,
                providerName,
                typeof(THandler).Name,
                resolved.StartPosition,
                resolved.MaxMessagesBeforeProcessing,
                resolved.MaxWaitTime,
                resolved.MaxRetries,
                resolved.RetryDelaySeconds))
            .AddHostedService<MessageStreamProcessingBackgroundService<TMessage, THandler>>();

        return flowlyBuilder.AddQueueRegistration(resolved.QueueName, providerName: providerName);
    }
}
