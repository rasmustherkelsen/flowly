using Flowly.MessageInfrastructure.Telemetry;
using OpenTelemetry.Trace;

namespace Flowly.OpenTelemetry;

/// <summary>
///     Extension methods for adding Flowly distributed tracing instrumentation to an OpenTelemetry
///     <see cref="TracerProviderBuilder" />.
/// </summary>
public static class FlowlyTracerProviderBuilderExtensions
{
    /// <summary>
    ///     Registers the Flowly activity source (<c>"Flowly"</c>) with the <see cref="TracerProviderBuilder" />, enabling
    ///     collection and export of all Flowly-emitted spans. The following span kinds are produced:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <b>Consumer spans</b> (<c>flowly.handle {queueName}</c>) — one span per message processed by a
    ///                 <c>MessageHandler&lt;T&gt;</c> or <c>EventHandlerBase&lt;TEvent&gt;</c>. Tags follow W3C messaging
    ///                 semantic conventions: <see cref="FlowlyInstrumentationConstants.MessagingSystem" />,
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingDestinationName" />,
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingOperationType" /> (<c>process</c>),
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingMessageId" />, and
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingMessageConversationId" />.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Producer spans</b> (<c>flowly.send {queueName}</c>) — one span per message dispatched via
    ///                 <c>IMessageSender</c>, <c>IJobMessageSender</c>, or <c>IEventSender</c>. Tags:
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingSystem" />,
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingDestinationName" />,
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingOperationType" /> (<c>publish</c>), and
    ///                 <see cref="FlowlyInstrumentationConstants.MessagingMessageId" />.
    ///             </description>
    ///         </item>
    ///     </list>
    ///     Requires <c>FlowlyOptions.EnableTelemetry</c> to be <see langword="true" /> (the default) in the Flowly
    ///     registration; otherwise no activities are started even if this method is called.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder" /> to add Flowly instrumentation to.</param>
    /// <returns>The same <see cref="TracerProviderBuilder" /> instance, for chaining.</returns>
    public static TracerProviderBuilder AddFlowlyInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(FlowlyInstrumentationConstants.ActivitySourceName);
}
