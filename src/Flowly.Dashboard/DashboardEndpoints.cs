using System.Reflection;
using Flowly.DeadLetters;
using Flowly.Jobs;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Dashboard;

internal static class DashboardEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints, FlowlyDashboardOptions options)
    {
        endpoints.MapGet("/api/submitters",  GetSubmitters);
        endpoints.MapPost("/api/submit",     PostSubmit);

        endpoints.MapGet("/api/jobs",            GetJobs);
        endpoints.MapGet("/api/recurring-jobs",  GetRecurringJobs);
        endpoints.MapPost("/api/recurring-jobs/{jobId}/trigger", TriggerRecurringJob);

        endpoints.MapGet("/api/dead-letters",                            GetDeadLetters);
        endpoints.MapPost("/api/dead-letters/{messageId}/requeue",       RequeueDeadLetter);
        endpoints.MapDelete("/api/dead-letters/{messageId}/discard",     DiscardDeadLetter);

        endpoints.MapGet("/api/config", (HttpContext ctx) =>
        {
            var hasJobs = ctx.RequestServices.GetService<IJobTrackingService>() is not null;
            var hasDeadLetters = ctx.RequestServices.GetService<IDeadLetterService>() is not null;
            var manifest = ctx.RequestServices.GetService<FlowlySubmitterManifest>();
            var hasSubmitters = manifest?.Submitters.Any(s => !s.QueueOrTopicName.StartsWith("flowlysys-")) ?? false;

            return Results.Ok(new { options.Title, options.PathPrefix, hasJobs, hasDeadLetters, hasSubmitters });
        });

        endpoints.MapFallback("{**slug}", ServeStaticAsset);
    }

    private static IResult GetSubmitters(HttpContext ctx)
    {
        var dispatcher = ctx.RequestServices.GetRequiredService<SubmitterDispatcher>();
        var result = dispatcher.Submitters
            .Where(s => !s.QueueOrTopicName.StartsWith("flowlysys-"))
            .Select(s => new
            {
                typeName     = s.TypeName,
                queueOrTopic = s.QueueOrTopicName,
                kind         = s.Kind.ToString().ToLowerInvariant(),
                schema       = s.JsonSchema
            });
        return Results.Ok(result);
    }

    private static async Task<IResult> PostSubmit(HttpContext ctx, CancellationToken ct)
    {
        var dispatcher = ctx.RequestServices.GetRequiredService<SubmitterDispatcher>();

        using var reader = new System.IO.StreamReader(ctx.Request.Body);
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

    private static async Task<IResult> GetDeadLetters(HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IDeadLetterService>();
        if (service is null)
            return Results.NotFound("Dead letter tracking is not configured in this application.");

        var letters = await service.GetDeadLetters(ct);
        return Results.Ok(letters);
    }

    private static async Task<IResult> RequeueDeadLetter(string messageId, HttpContext ctx, CancellationToken ct)
    {
        var service = ctx.RequestServices.GetService<IDeadLetterService>();
        if (service is null)
            return Results.NotFound("Dead letter tracking is not configured in this application.");

        try
        {
            await service.Requeue(messageId, cancellationToken: ct);
            return Results.Ok();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Dead letter message '{messageId}' not found.");
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
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
            _ when resourceName.EndsWith(".js")   => "application/javascript",
            _ when resourceName.EndsWith(".css")  => "text/css",
            _ when resourceName.EndsWith(".json") => "application/json",
            _ when resourceName.EndsWith(".svg")  => "image/svg+xml",
            _ when resourceName.EndsWith(".png")  => "image/png",
            _ when resourceName.EndsWith(".ico")  => "image/x-icon",
            _                                     => "application/octet-stream"
        };
}
