using Flowly.MessageStreams.DatabaseModel;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageStreams;

/// <summary>
///     Tracks the named <see cref="IDbContextFactory{TContext}" /> instances registered by
///     <c>Add&lt;Provider&gt;MessageStreamPersistence</c> calls. The first unnamed registration becomes the default;
///     a second unnamed registration throws. Mirrors the same "first is default" rule
///     <see cref="TransportRegistrationHelper.ResolveProviderName" /> already enforces for transport providers.
/// </summary>
internal sealed class MessageStreamCheckpointStoreRegistry
{
    private const string DefaultName = "default";

    private readonly Dictionary<string, IDbContextFactory<MessageStreamCheckpointDataContext>> _stores = new();

    public void Register(string? name, IDbContextFactory<MessageStreamCheckpointDataContext> factory)
    {
        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            _stores.Count,
            name,
            DefaultName,
            "Secondary message stream checkpoint persistence registrations must have an explicit name. " +
            "Pass name: \"...\" to Add<Provider>MessageStreamPersistence().");

        _stores[effectiveName] = factory;
    }

    public IDbContextFactory<MessageStreamCheckpointDataContext> Resolve(string? name)
    {
        if (name is null)
        {
            if (_stores.TryGetValue(DefaultName, out var defaultStore))
                return defaultStore;

            throw new InvalidOperationException(
                "No default message stream checkpoint persistence has been registered. Call " +
                "Add<Provider>MessageStreamPersistence() before UseMessageStreamCheckpoint(), or pass the name of " +
                "an explicitly named registration.");
        }

        if (_stores.TryGetValue(name, out var namedStore))
            return namedStore;

        throw new InvalidOperationException(
            $"No message stream checkpoint persistence named \"{name}\" has been registered. Registered names: " +
            $"{(_stores.Count == 0 ? "(none)" : string.Join(", ", _stores.Keys))}.");
    }

    public static MessageStreamCheckpointStoreRegistry GetOrCreate(IServiceCollection services)
    {
        var existing = services
            .Where(s => s.ServiceType == typeof(MessageStreamCheckpointStoreRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<MessageStreamCheckpointStoreRegistry>()
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var registry = new MessageStreamCheckpointStoreRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
