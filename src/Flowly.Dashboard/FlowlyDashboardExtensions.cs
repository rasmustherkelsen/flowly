using Flowly.Dashboard.Auth;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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
    public static IServiceCollection AddFlowlyDashboard(this IServiceCollection services, Action<FlowlyDashboardOptions>? configure = null)
    {
        var options = new FlowlyDashboardOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton(new FlowlySubmitterManifest());
        services.TryAddSingleton<SubmitterDispatcher>();

        if (options.Authentication is { } auth)
            RegisterAuth(services, auth);

        return services;
    }

    private static void RegisterAuth(IServiceCollection services, OAuthAuthenticationOptions auth)
    {
        services.AddHttpContextAccessor();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, DashboardAccessHandler>());

        services.AddAuthentication()
            .AddCookie(DashboardAuthConstants.CookieScheme, opts =>
            {
                opts.Cookie.Name = "flowly-dashboard";
                opts.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        if (IsApiRequest(ctx.Request))
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        return ctx.HttpContext.ChallengeAsync(DashboardAuthConstants.OidcScheme, ctx.Properties);
                    },
                    OnRedirectToAccessDenied = async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;

                        if (!IsApiRequest(ctx.Request))
                        {
                            ctx.Response.ContentType = "text/plain";
                            await ctx.Response.WriteAsync("You do not have permission to view this dashboard.");
                        }
                    }
                };
            })
            .AddOpenIdConnect(DashboardAuthConstants.OidcScheme, opts =>
            {
                opts.SignInScheme = DashboardAuthConstants.CookieScheme;
                opts.Authority = auth.Authority;
                opts.ClientId = auth.ClientId;
                opts.ClientSecret = auth.ClientSecret;
                opts.ResponseType = OpenIdConnectResponseType.Code;
                opts.ResponseMode = OpenIdConnectResponseMode.Query;
                opts.UsePkce = true;
                opts.CallbackPath = "/signin-oidc";
                opts.SaveTokens = true;
                opts.GetClaimsFromUserInfoEndpoint = true;
            });

        services.AddAuthorization(authz =>
        {
            authz.AddPolicy(DashboardAuthConstants.ViewerPolicy, BuildAccessPolicy(auth.ViewerRoles, auth.ViewerPolicies));
            authz.AddPolicy(DashboardAuthConstants.SubmitterPolicy, BuildAccessPolicy(auth.SubmitterRoles, auth.SubmitterPolicies));
        });
    }

    private static AuthorizationPolicy BuildAccessPolicy(IReadOnlyList<string>? roles, IReadOnlyList<string>? policyNames)
    {
        var builder = new AuthorizationPolicyBuilder(DashboardAuthConstants.CookieScheme);
        builder.RequireAuthenticatedUser();

        if ((roles?.Count ?? 0) > 0 || (policyNames?.Count ?? 0) > 0)
            builder.AddRequirements(new DashboardAccessRequirement(roles, policyNames));

        return builder.Build();
    }

    private static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api");

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

        ValidatePolicies(app, options);

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

            if (options.Authentication is not null)
            {
                dashboard.UseAuthentication();
                dashboard.UseAuthorization();
            }

            dashboard.UseEndpoints(endpoints => DashboardEndpoints.Map(endpoints, options));
        });

        return app;
    }

    private static void ValidatePolicies(IApplicationBuilder app, FlowlyDashboardOptions options)
    {
        if (options.Authentication is not { } auth)
            return;

        var allPolicyNames = (auth.ViewerPolicies ?? []).Concat(auth.SubmitterPolicies ?? []);
        var registered = app.ApplicationServices
            .GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        foreach (var policyName in allPolicyNames)
        {
            if (registered.GetPolicy(policyName) is null)
                throw new InvalidOperationException(
                    $"Flowly Dashboard: authorization policy '{policyName}' is not registered. " +
                    $"Register it with services.AddAuthorization(opts => opts.AddPolicy(\"{policyName}\", ...)) " +
                    $"before calling UseFlowlyDashboard.");
        }
    }
}