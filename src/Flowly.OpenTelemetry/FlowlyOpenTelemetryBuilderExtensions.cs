using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Flowly.OpenTelemetry;

/// <summary>
///     Convenience extensions for registering Flowly OpenTelemetry instrumentation on an
///     <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class FlowlyOpenTelemetryBuilderExtensions
{
    /// <summary>
    ///     Registers Flowly OpenTelemetry instrumentation — metrics and tracing — with the pipeline, and
    ///     pre-configures the OTel logging sink options (<c>IncludeFormattedMessage</c> and
    ///     <c>IncludeScopes</c>) so that log messages are exported with rendered placeholder values rather
    ///     than raw message templates when the caller later wires an exporter (e.g.
    ///     <c>builder.Services.AddOpenTelemetry().UseOtlpExporter()</c>).
    ///     Also equivalent to calling <c>.WithMetrics(m => m.AddFlowlyInstrumentation())</c> and
    ///     <c>.WithTracing(t => t.AddFlowlyInstrumentation())</c> on an existing
    ///     <see cref="OpenTelemetryBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to configure.</param>
    /// <returns>The same <see cref="IHostApplicationBuilder" />, for chaining.</returns>
    public static IHostApplicationBuilder AddFlowlyOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<OpenTelemetryLoggerOptions>(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());

        return builder;
    }
}
