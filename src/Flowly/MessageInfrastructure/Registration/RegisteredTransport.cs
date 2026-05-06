namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Represents transport that has been registered with the message bus client registry.
/// </summary>
/// <param name="Name">Name of the transport</param>
/// <param name="IsPrimary">Indicates whether this transport is the primary transport for the message bus client registry</param>
/// <param name="CreateTopologyOverride">Indicates whether the topology should be created for this transport</param>
public record RegisteredTransport(string Name, bool IsPrimary, bool? CreateTopologyOverride);