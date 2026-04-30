using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Model;

/// <summary>
///     Factory interface for creating handler settings for message handlers. This interface defines a method to create a
///     HandlerRegistration instance for a given handler type and message type, using the provided service collection to
///     resolve any necessary dependencies. The factory is responsible for determining the appropriate queue name, provider
///     name, handler name, concurrency settings, and retry policies based on the handler and message types, as well as any
///     configuration or conventions defined in the application. This allows for a flexible and extensible way to generate
///     handler settings that can be used to configure message processing behavior in the Flowly framework.
/// </summary>
public interface IHandlerSettingsFactory
{
    /// <summary>
    ///     Creates a HandlerRegistration instance for the specified handler type and message type, using the provided service
    ///     collection to resolve any necessary dependencies. The method is responsible for determining the appropriate queue
    ///     name, provider name, handler name, concurrency settings, and retry policies based on the handler and message types,
    ///     as well as any configuration or conventions defined in the application. The resulting HandlerRegistration instance
    ///     encapsulates all the necessary settings for configuring message processing behavior for the specified handler and
    ///     message types within the Flowly framework
    /// </summary>
    /// <param name="serviceCollection">
    ///     A valid IServiceCollection instance that can be used to resolve dependencies required
    ///     by the handler and message types
    /// </param>
    /// <typeparam name="THandler">The type of the message handler class that will process messages of type TMessage</typeparam>
    /// <typeparam name="TMessage">The type of the message that will be processed by the handler</typeparam>
    /// <returns>The HandlerRegistration instance containing the configured settings for message processing</returns>
    HandlerRegistration<TMessage> Create<THandler, TMessage>(IServiceCollection serviceCollection) where THandler : class;
}