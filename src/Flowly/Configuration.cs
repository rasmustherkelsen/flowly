namespace Flowly;

/// <summary>
///     Base class for configuring Flowly. Inherit from this class to set up your message handlers, transports, and other
///     Flowly components. This class combines <see cref="FlowlyDesignTimeFactory" /> (design-time queue discovery for CLI
///     tooling and Aspire integration) with <see cref="IFlowlyConfiguration" /> (runtime registration) into a single,
///     convenient entry point. Each deployable service should have exactly one class that inherits from this base class.
///     Implementations must provide a parameterless constructor.
/// </summary>
public abstract class Configuration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    /// <inheritdoc />
    public abstract void Configure(IFlowlyBuilder builder);
}
