using System.Diagnostics.CodeAnalysis;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.MessageStreams.SqlServer.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class MessageStreamCheckpointDataContextFactory : IDesignTimeDbContextFactory<MessageStreamCheckpointDataContext>
{
    private readonly MessageStreamCheckpointDataContext _dbContext;

    public MessageStreamCheckpointDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
        var connectionString = "Data Source=localhost;Initial Catalog=Flowly;User=sa;Password=Strong01;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString, options =>
        {
            options.EnableRetryOnFailure();
            options.MigrationsAssembly(typeof(MessageStreamCheckpointDataContextFactory).Assembly.GetName().Name);
        });
        _dbContext = new MessageStreamCheckpointDataContext(builder.Options);
    }

    public MessageStreamCheckpointDataContext CreateDbContext(string[] args) => _dbContext;
}
