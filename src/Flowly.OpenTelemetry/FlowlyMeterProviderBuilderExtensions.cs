using Flowly.MessageInfrastructure.Telemetry;
using OpenTelemetry.Metrics;

namespace Flowly.OpenTelemetry;

public static class FlowlyMeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddFlowlyInstrumentation(this MeterProviderBuilder builder)
        => builder.AddMeter(FlowlyInstrumentationConstants.MeterName);
}
