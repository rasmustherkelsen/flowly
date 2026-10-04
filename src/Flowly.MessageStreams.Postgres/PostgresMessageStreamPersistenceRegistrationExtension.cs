using Flowly.MessageStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering PostgreSQL-backed message stream checkpoint persistence.
/// </summary>
public static class PostgresMessageStreamPersistenceRegistrationExtension
{
    /// <summary>
    ///     Registers a message stream checkpoint persistence store backed by PostgreSQL. The first unnamed
    ///     registration becomes the default store used by <c>UseMessageStreamCheckpoint&lt;TMessage&gt;()</c> when no
    ///     name is given; a second unnamed registration throws <see cref="InvalidOperationException" />.
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder"/> to configure.</param>
    /// <param name="connection">
    ///     The name of a connection string in <c>IConfiguration</c> (looked up under <c>ConnectionStrings:</c>),
    ///     or a literal PostgreSQL connection string if no matching entry is found.
    /// </param>
    /// <param name="name">
    ///     An optional name distinguishing this store from other registered message stream persistence stores.
    ///     Omit to register it as the default store.
    /// </param>
    /// <param name="enableMigrations">
    ///     When <see langword="true"/> (the default), a hosted service applies pending EF Core migrations
    ///     automatically at application startup. Set to <see langword="false"/> to manage migrations externally.
    /// </param>
    /// <param name="configure">
    ///     An optional delegate to configure <see cref="MessageStreamCheckpointPersistenceOptions"/>.
    /// </param>
    /// <returns>The same <see cref="IFlowlyBuilder"/> instance, for fluent chaining.</returns>
    public static IFlowlyBuilder AddPostgresMessageStreamPersistence(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool enableMigrations = true,
        Action<MessageStreamCheckpointPersistenceOptions>? configure = null)
    {
        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;

        return flowlyBuilder.AddMessageStreamCheckpointPersistence(
            name,
            dbOptions => dbOptions.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                    npgsqlOptions.MigrationsAssembly(typeof(PostgresMessageStreamPersistenceRegistrationExtension).Assembly.GetName().Name);
                }),
            enableMigrations,
            configure);
    }
}
