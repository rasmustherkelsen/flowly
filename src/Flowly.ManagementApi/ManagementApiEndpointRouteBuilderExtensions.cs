using Flowly.ManagementApi.MessageStreams;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageStreams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.ManagementApi;

/// <summary>
///     Provides <see cref="UseFlowlyManagementApi" />, which maps the Flowly Management API's routes.
/// </summary>
public static class ManagementApiEndpointRouteBuilderExtensions
{
    /// <summary>
    ///     Maps the Flowly Management API: the built-in message stream checkpoint actions (only when a checkpoint
    ///     persistence store is registered — feature-detected the same way the Dashboard detects Jobs/DeadLetters),
    ///     the built-in message stream partition actions (list and resume, only when the host registered Flowly's stream partition state
    ///     services, which <c>AddFlowly</c> does), any externally-registered <see cref="IFlowlyManagementAction" />, and the OpenAPI discovery document.
    ///     Call this after <c>AddFlowlyManagementApi()</c> and after every <c>Add&lt;Provider&gt;MessageStreamPersistence</c>
    ///     call, since feature detection reads the built service provider.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map against (typically a <c>WebApplication</c>).</param>
    /// <returns>The same <paramref name="endpoints" /> instance, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <c>AddFlowlyManagementApi()</c> was not called, or when it was called without configuring an
    ///     authorization mode.
    /// </exception>
    public static IEndpointRouteBuilder UseFlowlyManagementApi(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetService<ManagementApiOptions>()
            ?? throw new InvalidOperationException(
                "Flowly Management API is not registered. Call services.AddFlowlyManagementApi() before UseFlowlyManagementApi().");

        var (readPolicyName, writePolicyName) = ResolvePolicyNames(options);

        MapBuiltInMessageStreamActions(endpoints, options.BasePath, readPolicyName, writePolicyName);
        MapBuiltInStreamPartitionActions(endpoints, options.BasePath, readPolicyName, writePolicyName);

        foreach (var action in endpoints.ServiceProvider.GetServices<IFlowlyManagementAction>())
        {
            var policyName = action.RequiredCapability == ManagementCapability.Write ? writePolicyName : readPolicyName;
            action.MapRoute(endpoints, options.BasePath, policyName);
        }

        var discoveryBuilder = endpoints.MapOpenApi($"{options.BasePath}/openapi.json");
        if (options.ProtectDiscoveryDocument)
            discoveryBuilder.RequireAuthorization(readPolicyName);

        if (options.EnableDocsUi)
            MapDocsUi(endpoints, options, readPolicyName);

        return endpoints;
    }

    private static (string ReadPolicyName, string WritePolicyName) ResolvePolicyNames(ManagementApiOptions options) =>
        options.AuthMode switch
        {
            ManagementApiAuthMode.ApiKey or ManagementApiAuthMode.OAuth2 =>
                (ManagementApiRegistrationExtensions.ReadPolicyName, ManagementApiRegistrationExtensions.WritePolicyName),
            ManagementApiAuthMode.ExistingPolicies => (options.ReadPolicyName!, options.WritePolicyName!),
            _ => throw new InvalidOperationException(
                "Flowly Management API has no authorization configured. Call RequireApiKey(), UseOAuth2(), or " +
                "RequireAuthorizationPolicies() when calling AddFlowlyManagementApi() — an unauthenticated " +
                "management surface is refused by default."),
        };

    private static void MapBuiltInMessageStreamActions(IEndpointRouteBuilder endpoints, string basePath, string readPolicyName, string writePolicyName)
    {
        var registry = endpoints.ServiceProvider.GetService<MessageStreamCheckpointStoreRegistry>();
        if (registry is null)
            return;

        new ListCheckpointsManagementAction(registry).MapRoute(endpoints, basePath, readPolicyName);
        new ResetCheckpointManagementAction(registry).MapRoute(endpoints, basePath, writePolicyName);
    }

    private static void MapBuiltInStreamPartitionActions(IEndpointRouteBuilder endpoints, string basePath, string readPolicyName, string writePolicyName)
    {
        var stateReader = endpoints.ServiceProvider.GetService<IStreamPartitionStateReader>();
        var resumer = endpoints.ServiceProvider.GetService<IStreamPartitionResumer>();
        if (stateReader is null || resumer is null)
            return;

        new ListStreamPartitionsManagementAction(stateReader).MapRoute(endpoints, basePath, readPolicyName);
        new ResumeStreamPartitionManagementAction(stateReader, resumer).MapRoute(endpoints, basePath, writePolicyName);
    }

    private static void MapDocsUi(IEndpointRouteBuilder endpoints, ManagementApiOptions options, string readPolicyName)
    {
        var builder = endpoints.MapGet($"{options.BasePath}/docs", () => Results.Content(BuildDocsHtml(options.BasePath), "text/html"));
        if (options.ProtectDiscoveryDocument)
            builder.RequireAuthorization(readPolicyName);
    }

    private static string BuildDocsHtml(string basePath) =>
        $"""
         <!doctype html>
         <html>
           <head>
             <title>Flowly Management API</title>
             <meta charset="utf-8" />
           </head>
           <body>
             <script id="api-reference" data-url="{basePath}/openapi.json"></script>
             <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
           </body>
         </html>
         """;
}
