using Flowly.MessageInfrastructure.Registration;

namespace Flowly.MessageInfrastructure.Model;

/// <summary>
///     Represents the registration details for a message handler, including its settings and the associated queue
///     registration information. This record is used internally to encapsulate all necessary information for setting up a
///     message handler in the Flowly framework. It includes the handler settings such as queue name, provider name,
///     handler name, concurrency and retry configurations, as well as the deferred queue registration details that specify
///     how the queue should be registered with the message broker.
/// </summary>
/// <param name="HandlerSettings">The handler settings for the message handler.</param>
/// <param name="QueueRegistration">The deferred queue registration details for the message handler.</param>
/// <typeparam name="TMessage">The message type that the handler processes.</typeparam>
internal record HandlerRegistration<TMessage>(
    IHandlerSettings<TMessage> HandlerSettings,
    DeferredQueueRegistration QueueRegistration);