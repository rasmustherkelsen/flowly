using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly;

/// <summary>
///     Provides <see cref="AddMessageStreamHealthCheck" />, which registers a health check over the stream partitions
///     owned by this process. It works for every stream transport (RabbitMQ, Azure Event Hubs and InMemory), since the
///     states it reports are maintained by the shared stream services and not by the transport.
/// </summary>
public static class MessageStreamHealthCheckRegistrationExtensions
{
    /// <summary>
    ///     The name the health check is registered under when none is given (<c>"flowly-message-streams"</c>).
    /// </summary>
    public const string DefaultMessageStreamHealthCheckName = "flowly-message-streams";

    /// <summary>
    ///     Registers a health check that reports <paramref name="failureStatus" /> (<see cref="HealthStatus.Unhealthy" /> by default)
    ///     while any <see cref="MessageStreamHandler{TMessage}" /> partition owned by this process is halted (its in-process retries
    ///     are exhausted, so it holds the failed batch and stopped consuming) or waiting (its start position could not be resolved, so it is not consuming),
    ///     and <see cref="HealthStatus.Healthy" /> otherwise, including while the process owns no stream partition. A non-partitioned
    ///     stream counts as one partition. The result's data lists each failing partition by queue, consumer and partition with its
    ///     state and last error. Requires the health checks to be mapped by the host, for example with <c>MapHealthChecks</c>.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder to register with.</param>
    /// <param name="name">The health check registration name. Defaults to <see cref="DefaultMessageStreamHealthCheckName" />.</param>
    /// <param name="failureStatus">
    ///     The status reported while a partition is halted or waiting. <see langword="null" /> reports
    ///     <see cref="HealthStatus.Unhealthy" />; pass <see cref="HealthStatus.Degraded" /> to keep the instance in rotation.
    /// </param>
    /// <param name="tags">Tags to filter the health check with. Defaults to <c>"message-streams"</c>.</param>
    /// <returns>The <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    public static IFlowlyBuilder AddMessageStreamHealthCheck(
        this IFlowlyBuilder flowlyBuilder,
        string name = DefaultMessageStreamHealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        StreamPartitionStateRegistry.GetOrCreate(flowlyBuilder.Services);

        flowlyBuilder.Services
            .AddHealthChecks()
            .AddCheck<StreamPartitionHealthCheck>(name, failureStatus, tags ?? ["message-streams"]);

        return flowlyBuilder;
    }
}
