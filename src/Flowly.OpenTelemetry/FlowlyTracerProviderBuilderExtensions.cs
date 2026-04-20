using Flowly.MessageInfrastructure.Telemetry;
using OpenTelemetry.Trace;

namespace Flowly.OpenTelemetry;

public static class FlowlyTracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddFlowlyInstrumentation(this TracerProviderBuilder builder)
        => builder.AddSource(FlowlyInstrumentationConstants.ActivitySourceName);
}
