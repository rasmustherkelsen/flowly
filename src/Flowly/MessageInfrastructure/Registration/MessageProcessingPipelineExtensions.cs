using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Provides low-level extension methods for wiring a custom <see cref="IMessageHandlingStrategy{TMessage}" />
///     directly into the message processing pipeline. Used internally by <see cref="FlowlyMessagePipelineCustomizationExtension" />
///     and transport-specific registration extensions; most application code should use higher-level helpers such as
///     <see cref="MessageHandlerRegistrationExtensions.AddMessageHandler{TMessage,THandler}" /> instead.
/// </summary>
public static class MessageProcessingPipelineExtensions
{
    /// <summary>
    ///     Registers a <see cref="MessageProcessingBackgroundService{TMessage}" /> as a hosted service, wiring it to the
    ///     supplied <paramref name="handlerSettings" /> and a strategy produced by <paramref name="strategyFactory" />.
    ///     Used by transport providers that need to supply a custom strategy at runtime rather than as a static type
    ///     parameter.
    /// </summary>
    /// <param name="services">The service collection to register the hosted service into.</param>
    /// <param name="handlerSettings">The resolved settings (queue name, concurrency, retry policy) for this handler.</param>
    /// <param name="strategyFactory">Factory that creates the <see cref="IMessageHandlingStrategy{TMessage}" /> from the DI container.</param>
    /// <typeparam name="TMessage">The message type to process.</typeparam>
    /// <returns>The same <see cref="IServiceCollection" /> for further registration.</returns>
    public static IServiceCollection AddMessageProcessingPipeline<TMessage>(
        this IServiceCollection services,
        IHandlerSettings<TMessage> handlerSettings,
        Func<IServiceProvider, IMessageHandlingStrategy<TMessage>> strategyFactory) where TMessage : class
    {
        return services.AddHostedService(sp => new MessageProcessingBackgroundService<TMessage>(
            sp.GetRequiredService<IMessageBusClientRegistry>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            handlerSettings,
            sp.GetRequiredService<ILogger<MessageProcessingBackgroundService<TMessage>>>(),
            sp.GetRequiredService<IHandlerInstrumentation>(),
            strategyFactory(sp)));
    }
}