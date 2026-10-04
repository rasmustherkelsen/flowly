using System.Diagnostics.CodeAnalysis;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.MessageStreams.Postgres.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class MessageStreamCheckpointDataContextFactory : IDesignTimeDbContextFactory<MessageStreamCheckpointDataContext>
{
    private readonly MessageStreamCheckpointDataContext _dbContext;

    public MessageStreamCheckpointDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
        var connectionString = "Host=localhost;Database=Flowly;Username=postgres;Password=postgres";
        builder.UseNpgsql(connectionString, options =>
        {
            options.EnableRetryOnFailure();
            options.MigrationsAssembly(typeof(MessageStreamCheckpointDataContextFactory).Assembly.GetName().Name);
        });
        _dbContext = new MessageStreamCheckpointDataContext(builder.Options);
    }

    public MessageStreamCheckpointDataContext CreateDbContext(string[] args) => _dbContext;
}
