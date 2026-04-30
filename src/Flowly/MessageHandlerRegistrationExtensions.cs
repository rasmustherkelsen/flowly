using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.MessageHandlingStrategies;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering message handlers in the Flowly framework.
/// </summary>
public static class MessageHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers a message handler for the specified message type.
    /// </summary>
    /// <param name="flowlyBuilder">A valid IFlowlyBuilder instance</param>
    /// <typeparam name="TMessage">The type of the message to handle</typeparam>
    /// <typeparam name="THandler">The type of the message handler</typeparam>
    /// <returns>A IMessageHandlerBuilder instance for further configuration</returns>
    public static IMessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : MessageHandler<TMessage>
        where TMessage : class
    {
        flowlyBuilder.Services.AddScoped<MessageHandler<TMessage>, THandler>();
        flowlyBuilder.AddMessageProcessor<TMessage, THandler, MessageProcessingBackgroundService<TMessage>, StandardMessageHandlingStrategy<TMessage>>();

        return new MessageHandlerBuilder<TMessage>(flowlyBuilder, flowlyBuilder.Services.BuildServiceProvider().GetRequiredService<IHandlerSettings<TMessage>>());
    }

    /// <summary>
    ///     Registers a batch message handler for the specified message type.
    /// </summary>
    /// <param name="flowlyBuilder">A valid IFlowlyBuilder instance</param>
    /// <typeparam name="TMessage">The type of the message to handle</typeparam>
    /// <typeparam name="THandler">The type of the batch message handler</typeparam>
    /// <returns>A IMessageHandlerBuilder instance for further configuration</returns>
    public static IFlowlyBuilder AddBatchMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : BatchMessageHandler<TMessage>
        where TMessage : class
    {
        flowlyBuilder.Services.AddScoped<BatchMessageHandler<TMessage>, THandler>();
        flowlyBuilder.AddMessageProcessor<TMessage, THandler, BatchProcessingBackgroundService<TMessage>>();

        return flowlyBuilder;
    }
}