using Flowly.Jobs.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flowly.Jobs.Tests.Helpers;

internal sealed class SqliteDbContextFactory : IDbContextFactory<JobStateDataContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JobStateDataContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<JobStateDataContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new JobStateDataContext(_options);
        context.Database.EnsureCreated();
    }

    public JobStateDataContext CreateDbContext() => new(_options);

    public Task<JobStateDataContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new JobStateDataContext(_options));

    public void Dispose() => _connection.Dispose();
}
