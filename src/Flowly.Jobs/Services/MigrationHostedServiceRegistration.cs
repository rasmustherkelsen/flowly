using System.Diagnostics.CodeAnalysis;
using Flowly.Jobs.DatabaseModel;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Jobs.Services;

[ExcludeFromCodeCoverage]
public static class MigrationHostedServiceRegistration
{
    public static IServiceCollection AddJobHandlerStateDatabaseMigrations(this IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService<JobStateDataContext>>();
        return services;
    }
}