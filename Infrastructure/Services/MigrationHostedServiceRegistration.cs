using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services;

public static class MigrationHostedServiceRegistration
{
    public static IServiceCollection AddJobHandlerStateDatabaseMigrations(this IServiceCollection services)
    {
        services.AddHostedService<MigrationHostedService<JobStateDataContext>>();
        return services;
    }
}