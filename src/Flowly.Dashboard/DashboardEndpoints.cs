using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Flowly.Dashboard.Auth;
using Flowly.DeadLetters;
using Flowly.Jobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Flowly.Dashboard;

internal static class DashboardEndpoints
{
    internal const string TotalCountHeader = "X-Total-Count";
    internal const int MaxDeadLetterPageSize = 1000;
    private const int MaxRequesterLength = 200;

    internal static void Map(IEndpointRouteBuilder endpoints, FlowlyDashboardOptions options)
    {
        var authEnabled = options.Authentication is not null;
        var mutationPolicy = GetMutationPolicy(options);

        var configEp = endpoints.MapGet("/api/config", GetConfig);
        var submittersEp = endpoints.MapGet("/api/submitters", GetSubmitters);
        var jobsEp = endpoints.MapGet("/api/jobs", GetJobs);
        var recurringEp = endpoints.MapGet("/api/recurring-jobs", GetRecurringJobs);
        var deadEp = endpoints.MapGet("/api/dead-letters", GetDeadLetters);

        var submitEp = endpoints.MapPost("/api/submit", PostSubmit);
        var triggerEp = endpoints.MapPost("/api/recurring-jobs/{jobId}/trigger", TriggerRecurringJob);
        var requeueEp = endpoints.MapPost("/api/dead-letters/{messageId}/requeue", RequeueDeadLetter);
        var discardEp = endpoints.MapDelete("/api/dead-letters/{messageId}/discard", DiscardDeadLetter);

        var spaEp = endpoints.MapFallback("{**slug}", ServeStaticAsset);

        if (authEnabled)
        {
            configEp.RequireAuthorization(DashboardAuthConstants.ViewerPolicy);
            submittersEp.RequireAuthorization(mutationPolicy);
            jobsEp.RequireAuthorization(DashboardAuthConstants.ViewerPolicy);
            recurringEp.RequireAuthorization(DashboardAuthConstants.ViewerPolicy);
            deadEp.RequireAuthorization(DashboardAuthConstants.ViewerPolicy);
            spaEp.RequireAuthorization(DashboardAuthConstants.ViewerPolicy);

            submitEp.RequireAuthorization(mutationPolicy);
            triggerEp.RequireAuthorization(mutationPolicy);
            requeueEp.RequireAuthorization(mutationPolicy);
            discardEp.RequireAuthorization(mutationPolicy);

            endpoints.MapGet("/logout", async ctx =>
            {
                await ctx.SignOutAsync(DashboardAuthConstants.CookieScheme);
                await ctx.SignOutAsync(
                    DashboardAuthConstants.OidcScheme,
                    new AuthenticationProperties { RedirectUri = options.PathPrefix + "/" });

                if (!ctx.Response.HasStarted)
                    ctx.Response.Redirect(options.PathPrefix + "/");
            }).AllowAnonymous();
        }
    }

    private static string GetMutationPolicy(FlowlyDashboardOptions options)
    {
        var auth = options.Authentication;
        var hasSubmitterRestriction = (auth?.SubmitterRoles?.Count ?? 0) > 0 || (auth?.SubmitterPolicies?.Count ?? 0) > 0;

        return hasSubmitterRestriction
            ? DashboardAuthConstants.SubmitterPolicy
            : DashboardAuthConstants.ViewerPolicy;
    }

    private static async Task<IResult> GetConfig(FlowlyDashboardOptions options, HttpContext ctx)
    {
        var hasJobs = ctx.RequestServices.GetService<IJobTrackingService>() is not null;
        var hasDeadLetters = ctx.RequestServices.GetService<IDeadLetterService>() is not null;
        var manifest = ctx.RequestServices.GetService<FlowlySubmitterManifest>();
        var hasSubmitters = manifest?.Submitters.Any(s => !s.QueueOrTopicName.StartsWith("flowlysys-")) ?? false;

        if (options.Authentication is null)
            return Results.Ok(new { options.Title, options.PathPrefix, hasJobs, hasDeadLetters, hasSubmitters });

        var authService = ctx.RequestServices.GetRequiredService<IAuthorizationService>();
        var canSubmit = (await authService.AuthorizeAsync(ctx.User, DashboardAuthConstants.SubmitterPolicy)).Succeeded;

        return Results.Ok(new
        {
            options.Title,
            options.PathPrefix,
            hasJobs,
            hasDeadLetters,
            hasSubmitters,
            authEnabled = true,
            canSubmit,
            logoutUrl = options.PathPrefix + "/logout"
        });
    }

    private static IResult GetSubmitters(HttpContext ctx)
    {
        var dispatcher = ctx.RequestServices.GetRequiredService<SubmitterDispatcher>();
        var result = dispatcher.Submitters
            .Where(s => !s.QueueOrTopicName.StartsWith("flowlysys-"))
            .Select(s => new
            {
                typeName = s.TypeName,
                queueOrTopic = s.QueueOrTopicName,
                kind = s.Kind.ToString().ToLowerInvariant(),
                schema = s.JsonSchema
            });
        return Results.Ok(result);
    }

    private static async Task<IResult> PostSubmit(HttpContext ctx, CancellationToken ct)
    {
        var dispatcher = ctx.RequestServices.GetRequiredService<SubmitterDispatcher>();

        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var doc = System.Text.Json.JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("type", out var typeEl) ||
            !doc.RootElement.TryGetProperty("payload", out var payloadEl))
            return Results.BadRequest("Request body must contain 'type' and 'payload' fields.");

        var typeName = typeEl.GetString();
        if (string.IsNullOrWhiteSpace(typeName))
            return Results.BadRequest("'type' must be a non-empty string.");

        var count = doc.RootElement.TryGetProperty("count", out var countEl) && countEl.TryGetInt32(out var c)
            ? Math.Clamp(c, 1, 1000)
            : 1;

        try
        {
            var payloadJson = payloadEl.GetRawText();
            var submitter = dispatcher.Submitters.FirstOrDefault(s => s.TypeName == typeName);
            var isSingleDispatch = submitter?.Kind is SubmitterKind.Call;

            if (isSingleDispatch || count == 1)
            {
                var response = await dispatcher.Dispatch(typeName, payloadJson, ctx.RequestServices, ct);
                return response is null ? Results.Ok() : Results.Ok(response);
            }

            await Task.WhenAll(Enumerable.Range(0, count)
                .Select(_ => dispatcher.Dispatch(typeName, payloadJson, ctx.RequestServices, ct)));

            return Results.Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(
                new { error = "The call timed out. The handler did not respond within the configured timeout period." },
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetJobs(HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IJobTrackingService>();
        if (service is null)
            return Results.NotFound("Job tracking is not configured in this application.");

        var jobs = await service.GetJobs(ct);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetRecurringJobs(HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IJobTrackingService>();
        if (service is null)
            return Results.NotFound("Job tracking is not configured in this application.");

        var jobs = await service.GetRecurringJobs(ct);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> TriggerRecurringJob(Guid jobId, HttpContext ctx, CancellationToken ct)
    {
        var sender = ctx.RequestServices.GetService<IJobMessageSender>();
        if (sender is null)
            return Results.NotFound("Job message sender is not configured in this application.");

        await sender.StartRecurringJob(jobId);
        return Results.Ok();
    }

    private static async Task<IResult> GetDeadLetters(HttpContext ctx, string? status, int? skip, int? take, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IDeadLetterService>();
        if (service is null)
            return Results.NotFound("Dead letter tracking is not configured in this application.");

        DeadLetterStatus? statusFilter = null;

        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<DeadLetterStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                return Results.BadRequest($"'{status}' is not a dead letter status. Use one of: {string.Join(", ", Enum.GetNames<DeadLetterStatus>())}.");

            statusFilter = parsed;
        }

        if (skip is < 0)
            return Results.BadRequest("skip must be zero or greater.");

        if (take is < 1 or > MaxDeadLetterPageSize)
            return Results.BadRequest($"take must be between 1 and {MaxDeadLetterPageSize}.");

        var page = await service.QueryDeadLetters(
            new DeadLetterQuery
            {
                Status = statusFilter,
                Skip = skip ?? 0,
                Take = take,
                IncludeTotalCount = take is not null
            },
            ct);

        if (page.TotalCount is { } totalCount)
            ctx.Response.Headers[TotalCountHeader] = totalCount.ToString(CultureInfo.InvariantCulture);

        return Results.Ok(page.Items);
    }

    private static async Task<IResult> RequeueDeadLetter(string messageId, HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IDeadLetterService>();
        if (service is null)
            return Results.NotFound("Dead letter tracking is not configured in this application.");

        if (!ctx.Request.HasJsonContentType() && HasRequestBody(ctx.Request))
            return Results.Problem(
                title: "Unsupported media type",
                detail: "The requeue request body must be sent as application/json.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);

        RequeueDeadLetterRequest? request;

        try
        {
            request = ctx.Request.HasJsonContentType() ? await ctx.Request.ReadFromJsonAsync<RequeueDeadLetterRequest>(ct) : null;
        }
        catch (JsonException)
        {
            return Results.BadRequest("The request body is not valid JSON.");
        }

        try
        {
            await service.Requeue(messageId, ResolveRequester(ctx.User), request?.MessageBody, ct);
            return Results.Ok();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Dead letter message '{messageId}' not found.");
        }
        catch (InvalidReplacementMessageBodyException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (DeadLetterStatusConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static bool HasRequestBody(HttpRequest request)
        => request.ContentLength > 0 || request.Headers.ContainsKey(HeaderNames.TransferEncoding);

    private static string? ResolveRequester(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        var requester = new[] { user.Identity.Name, user.FindFirst("sub")?.Value, user.FindFirst(ClaimTypes.NameIdentifier)?.Value }
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        return requester is { Length: > MaxRequesterLength } ? requester[..MaxRequesterLength] : requester;
    }

    private static async Task<IResult> DiscardDeadLetter(string messageId, HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IDeadLetterService>();
        if (service is null)
            return Results.NotFound("Dead letter tracking is not configured in this application.");

        try
        {
            await service.Discard(messageId, ct);
            return Results.Ok();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Dead letter message '{messageId}' not found.");
        }
        catch (DeadLetterStatusConflictException ex)
        {
            return Results.Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static Task ServeStaticAsset(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value?.TrimStart('/') ?? string.Empty;

        var resourceName = string.IsNullOrEmpty(path) || path == "index.html"
            ? "Flowly.Dashboard.wwwroot.index.html"
            : $"Flowly.Dashboard.wwwroot.{path.Replace('/', '.')}";

        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName)
                     ?? assembly.GetManifestResourceStream("Flowly.Dashboard.wwwroot.index.html");

        if (stream is null)
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        ctx.Response.ContentType = GetContentType(resourceName);
        return stream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    }

    private static string GetContentType(string resourceName) =>
        resourceName switch
        {
            _ when resourceName.EndsWith(".html") => "text/html; charset=utf-8",
            _ when resourceName.EndsWith(".js") => "application/javascript",
            _ when resourceName.EndsWith(".css") => "text/css",
            _ when resourceName.EndsWith(".json") => "application/json",
            _ when resourceName.EndsWith(".svg") => "image/svg+xml",
            _ when resourceName.EndsWith(".png") => "image/png",
            _ when resourceName.EndsWith(".ico") => "image/x-icon",
            _ => "application/octet-stream"
        };
}