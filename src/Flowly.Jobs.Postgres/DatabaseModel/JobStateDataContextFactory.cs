using System.Diagnostics.CodeAnalysis;
using Flowly.Jobs.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.Jobs.Postgres.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class JobStateDataContextFactory : IDesignTimeDbContextFactory<JobStateDataContext>
{
    private readonly JobStateDataContext _dbContext;

    public JobStateDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<JobStateDataContext>();
        var connectionString = "Host=localhost;Database=Flowly;Username=postgres;Password=postgres";
        builder.UseNpgsql(connectionString, options =>
        {
            options.EnableRetryOnFailure();
            options.MigrationsAssembly(typeof(JobStateDataContextFactory).Assembly.GetName().Name);
        });
        _dbContext = new JobStateDataContext(builder.Options);
    }

    public JobStateDataContext CreateDbContext(string[] args)
    {
        return _dbContext;
    }
}
