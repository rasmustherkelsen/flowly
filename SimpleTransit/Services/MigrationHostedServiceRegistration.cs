using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using SimpleTransit.DatabaseModel.JobStateDatabase;

namespace SimpleTransit.Services;

[ExcludeFromCodeCoverage]
internal static class MigrationHostedServiceRegistration
{
    public static IServiceCollection AddJobHandlerStateDatabaseMigrations(this IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService<JobStateDataContext>>();
        return services;
    }
}