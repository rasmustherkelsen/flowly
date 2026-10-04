using System.Diagnostics.CodeAnalysis;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.MessageStreams.SQLite.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class MessageStreamCheckpointDataContextFactory : IDesignTimeDbContextFactory<MessageStreamCheckpointDataContext>
{
    private readonly MessageStreamCheckpointDataContext _dbContext;

    public MessageStreamCheckpointDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
        builder.UseSqlite(
            "Data Source=flowly-message-streams.db",
            options => options.MigrationsAssembly(typeof(MessageStreamCheckpointDataContextFactory).Assembly.GetName().Name));
        _dbContext = new MessageStreamCheckpointDataContext(builder.Options);
    }

    public MessageStreamCheckpointDataContext CreateDbContext(string[] args) => _dbContext;
}
