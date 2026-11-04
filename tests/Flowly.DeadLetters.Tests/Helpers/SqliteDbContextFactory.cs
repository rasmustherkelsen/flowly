using Flowly.DeadLetters.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flowly.DeadLetters.Tests.Helpers;

internal sealed class SqliteDbContextFactory : IDbContextFactory<DeadLetterDataContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DeadLetterDataContext> _options;

    public SqliteDbContextFactory(params IInterceptor[] interceptors)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DeadLetterDataContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        using var context = new DeadLetterDataContext(_options);
        context.Database.EnsureCreated();
    }

    public DeadLetterDataContext CreateDbContext() => new(_options);

    public Task<DeadLetterDataContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DeadLetterDataContext(_options));

    public void Dispose() => _connection.Dispose();
}
