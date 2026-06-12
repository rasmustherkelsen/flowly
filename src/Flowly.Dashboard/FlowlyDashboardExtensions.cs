using Flowly.MessageInfrastructure.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly.Dashboard;

/// <summary>
///     Provides extension methods for registering and mounting the Flowly dashboard middleware.
/// </summary>
public static class FlowlyDashboardExtensions
{
    /// <summary>
    ///     Registers the Flowly dashboard services in the DI container.
    ///     Call this before <see cref="UseFlowlyDashboard" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configure">Optional delegate to configure <see cref="FlowlyDashboardOptions" />.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for further configuration.</returns>
    public static IServiceCollection AddFlowlyDashboard(
        this IServiceCollection services,
        Action<FlowlyDashboardOptions>? configure = null)
    {
        var options = new FlowlyDashboardOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton(new FlowlySubmitterManifest());
        services.TryAddSingleton<SubmitterDispatcher>();

        return services;
    }

    /// <summary>
    ///     Mounts the Flowly dashboard middleware at the path prefix configured via
    ///     <see cref="FlowlyDashboardOptions.PathPrefix" /> (default <c>/flowly</c>).
    ///     Registers all dashboard API endpoints and serves the embedded SPA for any unmatched path under the prefix.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder" /> to add the middleware to.</param>
    /// <returns>The same <see cref="IApplicationBuilder" /> for further configuration.</returns>
    public static IApplicationBuilder UseFlowlyDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<FlowlyDashboardOptions>();

        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.Value == options.PathPrefix)
            {
                ctx.Response.Redirect(options.PathPrefix + "/");
                return;
            }

            await next(ctx);
        });

        app.Map(options.PathPrefix, dashboard =>
        {
            dashboard.UseRouting();
            dashboard.UseEndpoints(endpoints => DashboardEndpoints.Map(endpoints, options));
        });

        return app;
    }
}
