using System.Diagnostics.CodeAnalysis;
using Flowly.DatabaseModel.JobStateDatabase;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Services;

[ExcludeFromCodeCoverage]
internal static class MigrationHostedServiceRegistration
{
    public static IServiceCollection AddJobHandlerStateDatabaseMigrations(this IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService<JobStateDataContext>>();
        return services;
    }
}