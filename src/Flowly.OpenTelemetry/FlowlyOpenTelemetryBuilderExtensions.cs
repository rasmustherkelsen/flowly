using Microsoft.Extensions.Configuration;
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
    /// <remarks>
    ///     The resource's <c>service.name</c> is left to the SDK: <c>OTEL_SERVICE_NAME</c>, a
    ///     <c>service.name</c> entry in <c>OTEL_RESOURCE_ATTRIBUTES</c>, or otherwise
    ///     <c>unknown_service:&lt;process name&gt;</c>. Use
    ///     <see cref="AddFlowlyOpenTelemetry(IHostApplicationBuilder, string)" /> to give the process a
    ///     default name in code.
    /// </remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to configure.</param>
    /// <returns>The same <see cref="IHostApplicationBuilder" />, for chaining.</returns>
    public static IHostApplicationBuilder AddFlowlyOpenTelemetry(this IHostApplicationBuilder builder)
    {
        AddFlowlyInstrumentation(builder);
        return builder;
    }

    /// <summary>
    ///     Registers Flowly OpenTelemetry instrumentation exactly like
    ///     <see cref="AddFlowlyOpenTelemetry(IHostApplicationBuilder)" /> and sets the resource's
    ///     <c>service.name</c> — the name tracing backends such as Jaeger or Zipkin list the process under —
    ///     to <paramref name="serviceName" />.
    /// </summary>
    /// <remarks>
    ///     <paramref name="serviceName" /> is a default, not an override: the standard OpenTelemetry
    ///     configuration still wins when it names the service, so a deployment can rename the process
    ///     without a code change. In order of precedence:
    ///     <list type="number">
    ///         <item><description><c>OTEL_SERVICE_NAME</c>, when set to a non-blank value;</description></item>
    ///         <item><description>a <c>service.name</c> entry in <c>OTEL_RESOURCE_ATTRIBUTES</c>;</description></item>
    ///         <item><description><paramref name="serviceName" />.</description></item>
    ///     </list>
    ///     Both variables are read from the host's <see cref="IConfiguration" /> when the providers are
    ///     built, so environment variables, <c>appsettings.json</c> and command-line arguments all count.
    ///     An empty or whitespace <c>OTEL_SERVICE_NAME</c> is treated as unset.
    /// </remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to configure.</param>
    /// <param name="serviceName">
    ///     The default service name for this process, for example <c>"MyApp.Receiver"</c>. Must not be
    ///     <see langword="null" />, empty or whitespace.
    /// </param>
    /// <returns>The same <see cref="IHostApplicationBuilder" />, for chaining.</returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="serviceName" /> is <see langword="null" />, empty or whitespace.
    /// </exception>
    public static IHostApplicationBuilder AddFlowlyOpenTelemetry(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        AddFlowlyInstrumentation(builder)
            .ConfigureResource(resource => resource.AddDetector(serviceProvider =>
                new ServiceNameResourceDetector(serviceName, serviceProvider.GetService<IConfiguration>())));

        return builder;
    }

    private static IOpenTelemetryBuilder AddFlowlyInstrumentation(IHostApplicationBuilder builder)
    {
        builder.Services.Configure<OpenTelemetryLoggerOptions>(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        return builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddFlowlyInstrumentation())
            .WithTracing(t => t.AddFlowlyInstrumentation());
    }
}
