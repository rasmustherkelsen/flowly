using Flowly.MessageInfrastructure.Telemetry;
using OpenTelemetry.Metrics;

namespace Flowly.OpenTelemetry;

/// <summary>
///     Extension methods for adding Flowly metrics instrumentation to an OpenTelemetry <see cref="MeterProviderBuilder" />.
/// </summary>
public static class FlowlyMeterProviderBuilderExtensions
{
    /// <summary>
    ///     Registers the Flowly meter (<c>"Flowly"</c>) with the <see cref="MeterProviderBuilder" />, enabling collection and
    ///     export of all Flowly-specific metrics instruments. The following instrument groups are included:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <b>Handler</b> — <see cref="FlowlyInstrumentationConstants.HandlerMessagesReceived" />,
    ///                 <see cref="FlowlyInstrumentationConstants.HandlerMessagesSucceeded" />,
    ///                 <see cref="FlowlyInstrumentationConstants.HandlerMessagesFailed" />,
    ///                 <see cref="FlowlyInstrumentationConstants.HandlerMessagesRetried" />,
    ///                 <see cref="FlowlyInstrumentationConstants.HandlerMessagesHalted" />,
    ///                 <see cref="FlowlyInstrumentationConstants.HandlerProcessingDuration" />
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Submitter</b> — <see cref="FlowlyInstrumentationConstants.SubmitterMessagesSent" />,
    ///                 <see cref="FlowlyInstrumentationConstants.SubmitterMessagesFailed" />,
    ///                 <see cref="FlowlyInstrumentationConstants.SubmitterSendDuration" />
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Event handler</b> — <see cref="FlowlyInstrumentationConstants.EventHandlerMessagesReceived" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventHandlerMessagesSucceeded" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventHandlerMessagesFailed" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventHandlerMessagesRetried" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventHandlerProcessingDuration" />
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Event publisher</b> — <see cref="FlowlyInstrumentationConstants.EventPublisherEventsRaised" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventPublisherEventsFailed" />,
    ///                 <see cref="FlowlyInstrumentationConstants.EventPublisherRaiseDuration" />
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Dead letters</b> — <see cref="FlowlyInstrumentationConstants.DeadLettersPending" /> (gauge)
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <b>Jobs</b> — <see cref="FlowlyInstrumentationConstants.JobsFailed" />,
    ///                 <see cref="FlowlyInstrumentationConstants.JobsRunning" /> (gauges)
    ///             </description>
    ///         </item>
    ///     </list>
    ///     Requires <c>FlowlyOptions.EnableTelemetry</c> to be <see langword="true" /> (the default) in the Flowly
    ///     registration; otherwise no instruments are emitted even if this method is called.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder" /> to add Flowly instrumentation to.</param>
    /// <returns>The same <see cref="MeterProviderBuilder" /> instance, for chaining.</returns>
    public static MeterProviderBuilder AddFlowlyInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(FlowlyInstrumentationConstants.MeterName);
}