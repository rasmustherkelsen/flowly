namespace Flowly;

/// <summary>
///     Interface for configuring Flowly. Implement this interface to set up your message handlers, transports, and other
///     Flowly components.
/// </summary>
public interface IFlowlyConfiguration
{
    /// <summary>
    ///     Configures Flowly services and settings using the provided builder. This method is called during application
    ///     startup, allowing you to register message handlers, configure transports, and set up any other necessary components
    ///     for Flowly to function properly. Use the builder to access the service collection and configuration for flexible
    ///     setup of your Flowly components.
    /// </summary>
    /// <param name="builder"></param>
    void Configure(IFlowlyBuilder builder);
}