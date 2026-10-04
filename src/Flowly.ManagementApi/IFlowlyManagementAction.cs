using Microsoft.AspNetCore.Routing;

namespace Flowly.ManagementApi;

/// <summary>
///     A single capability exposed through the Flowly Management API. Register implementations into DI
///     (<c>services.AddSingleton&lt;IFlowlyManagementAction, MyAction&gt;()</c>) and <see cref="ManagementApiEndpointRouteBuilderExtensions.UseFlowlyManagementApi" />
///     discovers and maps every one of them, in addition to the built-in message stream checkpoint actions it
///     feature-detects itself. This is the extensibility point future Flowly features (pause/resume a handler,
///     requeue a dead letter, retry a job, …) plug into — nothing about discovery, routing, or authorization needs
///     revisiting as the API grows.
/// </summary>
public interface IFlowlyManagementAction
{
    /// <summary>
    ///     The feature area this action belongs to (e.g. <c>"message-streams"</c>). Used both as the route segment
    ///     actions nest under (<c>{BasePath}/{FeatureArea}/...</c>) and as the OpenAPI tag, so the generated
    ///     discovery document groups actions by the feature that owns them.
    /// </summary>
    string FeatureArea { get; }

    /// <summary>
    ///     The authorization tier this action requires. <see cref="ManagementApiEndpointRouteBuilderExtensions.UseFlowlyManagementApi" />
    ///     applies the matching configured policy automatically — implementations do not call
    ///     <c>RequireAuthorization</c> themselves.
    /// </summary>
    ManagementCapability RequiredCapability { get; }

    /// <summary>
    ///     Maps this action's route(s) against <paramref name="endpoints" />. Implementations map a normal minimal
    ///     API endpoint (nested under <paramref name="basePath" /> plus <see cref="FeatureArea" />) and apply
    ///     <c>.WithSummary()</c>/<c>.WithDescription()</c>/<c>.WithTags(FeatureArea)</c> so the OpenAPI document is
    ///     genuinely useful — but must <em>not</em> call <c>.RequireAuthorization()</c> themselves;
    ///     <paramref name="authorizationPolicyName" /> is the policy this action's route must be gated behind.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map against.</param>
    /// <param name="basePath">The configured <see cref="ManagementApiOptions.BasePath" />.</param>
    /// <param name="authorizationPolicyName">The authorization policy name matching <see cref="RequiredCapability" />.</param>
    void MapRoute(IEndpointRouteBuilder endpoints, string basePath, string authorizationPolicyName);
}
