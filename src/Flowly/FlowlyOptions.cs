using Flowly.MessageInfrastructure;

namespace Flowly;

/// <summary>
///     Options for configuring Flowly's behavior. These options can be set when initializing the Flowly system and will
///     affect how Flowly creates topologies and handles telemetry.
/// </summary>
public class FlowlyOptions
{
    /// <summary>
    ///     Determines whether Flowly should automatically create the necessary topologies (e.g., queues, topics, exchanges) in
    ///     the underlying messaging system when the system is initialized. If set to true, Flowly will attempt to create the
    ///     required topologies based on the defined flows and event types. If set to false, Flowly will assume that the
    ///     necessary topologies already exist and will not attempt to create them. This option can be useful in scenarios
    ///     where the topologies are managed separately (e.g., by an infrastructure team) or when using a messaging system that
    ///     does not support automatic topology creation.
    /// </summary>
    public bool CreateTopology { get; set; } = true;

    /// <summary>
    ///     Determines whether Flowly should enable telemetry for monitoring and diagnostics. If set to true, Flowly will
    ///     collect and emit telemetry data such as metrics, logs, and traces related to the execution of flows and event
    ///     handling. This can be useful for monitoring the health and performance of the system, diagnosing issues, and
    ///     gaining insights into how flows are being executed. If set to false, Flowly will not collect or emit any telemetry
    ///     data. This option can be useful in scenarios where telemetry is not needed or when using a custom telemetry
    ///     solution that is not compatible with Flowly's built-in telemetry features.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    ///     The name of this application instance. Required when registering a call submitter with
    ///     <c>AddCallSubmitter&lt;TMessage&gt;()</c>. Used to derive a unique reply queue name in the format
    ///     <c>{callQueue}.reply.{InstanceName}</c>. Flowly throws <see cref="InvalidOperationException" /> at
    ///     startup if a call submitter is registered without this value being set.
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    ///     The global default timeout for <see cref="IMessageCaller.Call{TMessage,TReturn}" /> operations. Applied
    ///     when no per-submitter timeout is configured via <see cref="CallSubmitterOptions.Timeout" />.
    ///     Defaults to 2 minutes.
    /// </summary>
    public TimeSpan MessageCallTimeout { get; set; } = TimeSpan.FromMinutes(2);

    internal Type TopologyNameResolverType { get; private set; } = typeof(KebabCaseTopologyNameResolver);

    /// <summary>
    ///     Overrides the default <see cref="ITopologyNameResolver" /> used to derive broker-level names for queues, event
    ///     topics, and event subscriptions. The resolver must have a public parameterless constructor. The default resolver
    ///     is <see cref="KebabCaseTopologyNameResolver" />.
    /// </summary>
    /// <typeparam name="TResolver">
    ///     The custom resolver type. Must implement <see cref="ITopologyNameResolver" /> and have a public parameterless
    ///     constructor.
    /// </typeparam>
    public void WithTopologyNameResolver<TResolver>() where TResolver : ITopologyNameResolver, new()
        => TopologyNameResolverType = typeof(TResolver);
}