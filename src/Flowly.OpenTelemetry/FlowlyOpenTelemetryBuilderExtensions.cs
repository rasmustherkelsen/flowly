using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

namespace Flowly.OpenTelemetry;

/// <summary>
///     Convenience extensions for registering Flowly OpenTelemetry instrumentation on an
///     <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class FlowlyOpenTelemetryBuilderExtensions
{
    /// <summary>
    ///     Registers Flowly metrics and tracing instrumentation with the OpenTelemetry pipeline.
    ///     Equivalent to calling <c>.WithMetrics(m => m.AddFlowlyInstrumentation())</c> and
    ///     <c>.WithTracing(t => t.AddFlowlyInstrumentation())</c> on an existing
    ///     <see cref="OpenTelemetryBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to configure.</param>
    /// <returns>The same <see cref="IHostApplicationBuilder" />, for chaining.</returns>
    public static IHostApplicationBuilder AddFlowlyOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());

        return builder;
    }
}
