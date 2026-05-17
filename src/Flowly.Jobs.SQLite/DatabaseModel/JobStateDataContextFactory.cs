using System.Diagnostics.CodeAnalysis;
using Flowly.Jobs.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.Jobs.SQLite.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class JobStateDataContextFactory : IDesignTimeDbContextFactory<JobStateDataContext>
{
    private readonly JobStateDataContext _dbContext;

    public JobStateDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<JobStateDataContext>();
        builder.UseSqlite(
            "Data Source=flowly-jobs.db",
            options => options.MigrationsAssembly(typeof(JobStateDataContextFactory).Assembly.GetName().Name));
        _dbContext = new JobStateDataContext(builder.Options);
    }

    public JobStateDataContext CreateDbContext(string[] args) => _dbContext;
}
