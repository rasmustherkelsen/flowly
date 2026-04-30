using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure;

/// <summary>
///     Provides extension methods for customizing the message processing pipeline for job handlers. This is used
///     internally by the Flowly framework to set up the necessary infrastructure for processing job messages, but can also
///     be used by developers to customize the message processing pipeline for specific scenarios.
/// </summary>
public static class FlowlyMessagePipelineCustomizationExtension
{
    /// <summary>
    ///     Registers a message processor for the specified message type, handler, and handling strategy. This is used
    ///     internally for job handlers but can be used for custom scenarios where the default message processing pipeline
    ///     needs to be customized.
    /// </summary>
    /// <param name="flowlyBuilder">A <see cref="IFlowlyBuilder" /> instance used to configure Flowly services.</param>
    /// <typeparam name="TMessage">The type of job message to process.</typeparam>
    /// <typeparam name="THandler">The type of message handler to use.</typeparam>
    /// <typeparam name="TStrategy">The type of message handling strategy to use.</typeparam>
    /// <typeparam name="TMessageProcessingBackgroundService">The background service responsible for processing messages.</typeparam>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance.</returns>
    public static IFlowlyBuilder AddMessageProcessor<TMessage, THandler, TMessageProcessingBackgroundService, TStrategy>(this IFlowlyBuilder flowlyBuilder)
        where TMessage : class
        where THandler : class
        where TMessageProcessingBackgroundService : MessageProcessingBackgroundServiceBase<TMessage>
        where TStrategy : class, IMessageHandlingStrategy<TMessage>
    {
        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var handlerSettingsFactory = serviceProvider.GetRequiredService<IHandlerSettingsFactory>();
        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();

        var registration = handlerSettingsFactory.Create<THandler, TMessage>(flowlyBuilder.Services);

        queueRegistrar.Register(flowlyBuilder.Services, registration.QueueRegistration, registration.HandlerSettings.ProviderName);

        flowlyBuilder.Services
            .AddSingleton(registration.HandlerSettings)
            .AddTransient<THandler>()
            .AddTransient<IMessageHandlingStrategy<TMessage>, TStrategy>()
            .AddHostedService<TMessageProcessingBackgroundService>();

        return flowlyBuilder;
    }

    /// <summary>
    ///     Registers a message processor for the specified message type, handler, and handling strategy. This is used
    ///     internally for job handlers but can be used for custom scenarios where the default message processing pipeline
    ///     needs to be customized.
    /// </summary>
    /// <param name="flowlyBuilder">A <see cref="IFlowlyBuilder" /> instance used to configure Flowly services.</param>
    /// <typeparam name="TMessage">The type of job message to process.</typeparam>
    /// <typeparam name="THandler">The type of message handler to use.</typeparam>
    /// <typeparam name="TMessageProcessingBackgroundService">The type of message processing background service to use.</typeparam>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance.</returns>
    public static IFlowlyBuilder AddMessageProcessor<TMessage, THandler, TMessageProcessingBackgroundService>(this IFlowlyBuilder flowlyBuilder)
        where TMessage : class
        where THandler : class
        where TMessageProcessingBackgroundService : MessageProcessingBackgroundServiceBase<TMessage>
    {
        using var serviceProvider = flowlyBuilder.Services.BuildServiceProvider();
        var handlerSettingsFactory = serviceProvider.GetRequiredService<IHandlerSettingsFactory>();
        var queueRegistrar = serviceProvider.GetRequiredService<IQueueRegistrar>();

        var registration = handlerSettingsFactory.Create<THandler, TMessage>(flowlyBuilder.Services);

        queueRegistrar.Register(flowlyBuilder.Services, registration.QueueRegistration, registration.HandlerSettings.ProviderName);

        flowlyBuilder.Services
            .AddSingleton(registration.HandlerSettings)
            .AddTransient<THandler>()
            .AddHostedService<TMessageProcessingBackgroundService>();

        return flowlyBuilder;
    }
}