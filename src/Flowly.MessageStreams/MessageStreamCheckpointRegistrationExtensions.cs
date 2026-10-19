using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageStreams.DatabaseModel;
using Flowly.MessageStreams.Services;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageStreams;

/// <summary>
///     Provides the shared registration path every built-in message stream checkpoint persistence provider
///     (<c>Flowly.MessageStreams.SqlServer</c>, <c>.Postgres</c>, <c>.SQLite</c>) calls into, plus
///     <see cref="UseMessageStreamCheckpoint{TMessage}" />, which opts a message type's stream handler into using one
///     of those registered stores.
/// </summary>
public static class MessageStreamCheckpointRegistrationExtensions
{
    /// <summary>
    ///     Registers a named message stream checkpoint persistence store. Called by each provider's
    ///     <c>Add&lt;Provider&gt;MessageStreamPersistence</c> method with a provider-specific <c>DbContextOptionsBuilder</c>
    ///     configuration delegate. The first unnamed registration becomes the default store; a second unnamed
    ///     registration throws <see cref="InvalidOperationException" />.
    /// </summary>
    internal static IFlowlyBuilder AddMessageStreamCheckpointPersistence(
        this IFlowlyBuilder flowlyBuilder,
        string? name,
        Action<DbContextOptionsBuilder> dbContextOptions,
        bool enableMigrations,
        Action<MessageStreamCheckpointPersistenceOptions>? configure)
    {
        if (configure is not null)
            flowlyBuilder.Services.Configure(configure);

        var optionsBuilder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
        dbContextOptions(optionsBuilder);
        var dbContextFactory = new PooledDbContextFactory<MessageStreamCheckpointDataContext>(optionsBuilder.Options);

        MessageStreamCheckpointStoreRegistry.GetOrCreate(flowlyBuilder.Services).Register(name, dbContextFactory);

        if (enableMigrations)
            flowlyBuilder.Services.AddSingleton<IHostedService>(sp =>
                new MigrationHostedService(dbContextFactory, sp.GetRequiredService<ILogger<MigrationHostedService>>()));

        return flowlyBuilder;
    }

    /// <summary>
    ///     Opts <typeparamref name="TMessage" />'s stream handler into restart-survival backed by a built-in
    ///     persistence store registered earlier via <c>Add&lt;Provider&gt;MessageStreamPersistence</c>. Can be called
    ///     either before or after <see cref="MessageStreamHandlerRegistrationExtensions.AddMessageStreamHandler{TMessage,THandler}" />.
    ///     Throws <see cref="InvalidOperationException" /> when the resolved provider for <typeparamref name="TMessage" />
    ///     is InMemory (checkpointing is RabbitMQ-only), or when <paramref name="name" /> does not match a registered
    ///     persistence store.
    /// </summary>
    /// <typeparam name="TMessage">The message contract type whose stream position should be persisted.</typeparam>
    /// <param name="flowlyBuilder">The Flowly builder to register with.</param>
    /// <param name="name">
    ///     The name of the persistence store to use, as passed to <c>Add&lt;Provider&gt;MessageStreamPersistence</c>.
    ///     Omit to use the default (unnamed) store.
    /// </param>
    /// <returns>The <see cref="IFlowlyBuilder" /> for further configuration.</returns>
    public static IFlowlyBuilder UseMessageStreamCheckpoint<TMessage>(this IFlowlyBuilder flowlyBuilder, string? name = null)
        where TMessage : class
    {
        var providerName = ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(flowlyBuilder.Services, typeof(TMessage));
        var client = ProviderNameResolver.GetRegistry(flowlyBuilder.Services).GetTransportClient(providerName);

        if (client.MessagingSystem == "in-memory")
            throw new InvalidOperationException(
                $"UseMessageStreamCheckpoint<{typeof(TMessage).Name}>() was called, but InMemory-backed streams do not " +
                "support checkpointing — the underlying log has no cross-restart persistence of its own, so a persisted " +
                "position would point at data that no longer exists after a restart.");

        var dbContextFactory = MessageStreamCheckpointStoreRegistry.GetOrCreate(flowlyBuilder.Services).Resolve(name);
        var queueName = flowlyBuilder.TopologyNameResolver.ResolveQueueName<TMessage>();

        flowlyBuilder.Services.AddSingleton<MessageStreamCheckpoint<TMessage>>(
            new EfCoreMessageStreamCheckpoint<TMessage>(dbContextFactory, queueName));

        return flowlyBuilder;
    }
}
