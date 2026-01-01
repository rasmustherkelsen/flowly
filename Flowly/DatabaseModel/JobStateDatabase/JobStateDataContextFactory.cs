using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowly.DatabaseModel.JobStateDatabase;

[ExcludeFromCodeCoverage]
internal class JobStateDataContextFactory : IDesignTimeDbContextFactory<JobStateDataContext>
{
    private readonly JobStateDataContext _dbContext;

    public JobStateDataContextFactory()
    {
        var builder = new DbContextOptionsBuilder<JobStateDataContext>();
        var connectionString = "Data Source=localhost;Initial Catalog=Flowly;User=sa;Password=Strong01;TrustServerCertificate=True";
        builder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
        _dbContext = new JobStateDataContext(builder.Options);
    }

    public JobStateDataContext CreateDbContext(string[] args)
    {
        return _dbContext;
    }
}