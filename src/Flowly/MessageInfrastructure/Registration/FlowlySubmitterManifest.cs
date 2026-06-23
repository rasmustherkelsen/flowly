using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     A singleton that collects all submitter registrations made via <c>AddMessageSubmitter&lt;T&gt;</c>,
///     <c>AddEventSubmitter&lt;T&gt;</c>, and <c>AddCallSubmitter&lt;T&gt;</c>. Consumed by
///     <c>Flowly.Dashboard</c> to expose a list of sendable message types with their JSON schemas.
/// </summary>
public sealed class FlowlySubmitterManifest
{
    private readonly List<DeferredSubmitterRegistration> _submitters = [];

    /// <summary>
    ///     The deduplicated list of submitter registrations collected during application configuration.
    /// </summary>
    public IReadOnlyList<DeferredSubmitterRegistration> Submitters => _submitters;

    /// <summary>
    ///     Adds a submitter registration to the manifest. Duplicate registrations for the same
    ///     <see cref="DeferredSubmitterRegistration.MessageType" /> are silently ignored.
    /// </summary>
    /// <param name="registration">The submitter registration to add.</param>
    public void Add(DeferredSubmitterRegistration registration)
    {
        if (_submitters.Any(s => s.MessageType == registration.MessageType))
            return;

        _submitters.Add(registration);
    }

    internal static FlowlySubmitterManifest GetOrCreate(IServiceCollection services)
    {
        var existing = services
            .Where(s => s.ImplementationInstance is FlowlySubmitterManifest)
            .Select(s => (FlowlySubmitterManifest)s.ImplementationInstance!)
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var manifest = new FlowlySubmitterManifest();
        services.AddSingleton(manifest);
        return manifest;
    }
}
