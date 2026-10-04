using Flowly.ManagementApi.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.ManagementApi;

/// <summary>
///     Provides extension methods for registering the Flowly Management API — an opt-in, machine-oriented HTTP
///     surface for operational actions against a running Flowly host, independent of <c>Flowly.Dashboard</c>.
/// </summary>
public static class ManagementApiRegistrationExtensions
{
    internal const string ReadPolicyName = "FlowlyManagementRead";
    internal const string WritePolicyName = "FlowlyManagementWrite";

    /// <summary>
    ///     Registers the Flowly Management API services. Call this before
    ///     <see cref="ManagementApiEndpointRouteBuilderExtensions.UseFlowlyManagementApi" />. Exactly one of
    ///     <see cref="ManagementApiOptions.RequireApiKey(string)" />, <see cref="ManagementApiOptions.UseOAuth2" />, or
    ///     <see cref="ManagementApiOptions.RequireAuthorizationPolicies" /> must be configured — otherwise
    ///     <c>UseFlowlyManagementApi()</c> throws at startup.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configure">Delegate to configure <see cref="ManagementApiOptions" />, including authorization.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for further configuration.</returns>
    public static IServiceCollection AddFlowlyManagementApi(this IServiceCollection services, Action<ManagementApiOptions> configure)
    {
        var options = new ManagementApiOptions();
        configure(options);
        services.AddSingleton(options);

        services.AddOpenApi();

        switch (options.AuthMode)
        {
            case ManagementApiAuthMode.ApiKey:
                services
                    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
                    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                        ApiKeyAuthenticationHandler.SchemeName,
                        o =>
                        {
                            o.ReadApiKey = options.ReadApiKey!;
                            o.WriteApiKey = options.WriteApiKey!;
                        });

                services.AddAuthorizationBuilder()
                    .AddPolicy(ReadPolicyName, policy => policy.RequireRole("Flowly.Management.Read"))
                    .AddPolicy(WritePolicyName, policy => policy.RequireRole("Flowly.Management.Write"));
                break;

            case ManagementApiAuthMode.OAuth2:
                var oauth = options.OAuth!;

                services
                    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(o =>
                    {
                        o.Authority = oauth.Authority;
                        o.Audience = oauth.Audience;
                    });

                services.AddSingleton<IAuthorizationHandler, RequirementClaimHandler>();
                services.AddAuthorizationBuilder()
                    .AddPolicy(ReadPolicyName, policy => policy.Requirements.Add(new RequirementClaimRequirement(oauth.ReadRequirement)))
                    .AddPolicy(WritePolicyName, policy => policy.Requirements.Add(new RequirementClaimRequirement(oauth.WriteRequirement)));
                break;

            case ManagementApiAuthMode.ExistingPolicies:
            case ManagementApiAuthMode.None:
            default:
                // ExistingPolicies defers to policies the host already configured — nothing to register here.
                // None is validated (and rejected) in UseFlowlyManagementApi(), not here, so configuration errors
                // surface at the same point as every other Flowly startup validation.
                break;
        }

        return services;
    }
}
