using System.Diagnostics.CodeAnalysis;
using Flowly.Jobs.DatabaseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.Jobs.SqlServer.DatabaseModel;

[ExcludeFromCodeCoverage]
internal class JobStateDataContextFactory : IDesignTimeDbContextFactory<JobStateDataContext>
{
    private readonly JobStateDataContext _dbContext;

    public JobStateDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<JobStateDataContext>();
        var connectionString = "Data Source=localhost;Initial Catalog=Flowly;User=sa;Password=Strong01;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString, options =>
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
