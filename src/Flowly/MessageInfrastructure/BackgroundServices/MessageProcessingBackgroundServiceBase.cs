using Flowly.Transport;
using Microsoft.Extensions.Hosting;

namespace Flowly.MessageInfrastructure.BackgroundServices;

/// <summary>
///     Base class for Flowly message processing background services. Inherits <see cref="BackgroundService" /> and
///     constrains the message type to a reference type. Concrete implementations (e.g.
///     <see cref="MessageProcessingBackgroundService{TMessage}" />) are registered as hosted services by the handler
///     registration extensions and own the lifetime of a single <see cref="IMessageBusProcessor{TMessage}" />.
/// </summary>
/// <typeparam name="TMessage">The message type processed by this background service. Must be a reference type.</typeparam>
public abstract class MessageProcessingBackgroundServiceBase<TMessage> : BackgroundService where TMessage : class;