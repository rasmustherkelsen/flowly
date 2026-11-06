using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Http;

/// <summary>
///     Calls the Flowly Management API on behalf of a <see cref="FlowlyConnection" />, transparently acquiring and
///     refreshing an Entra ID client-credentials token when the connection uses OAuth2.
/// </summary>
internal static class FlowlyApiClient
{
    public static async Task<IReadOnlyList<CheckpointDto>> ListCheckpoints(FlowlyConnection connection, string? queue, string? consumer, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var query = string.Join("&", new[]
            {
                queue is not null ? $"queue={Uri.EscapeDataString(queue)}" : null,
                consumer is not null ? $"consumer={Uri.EscapeDataString(consumer)}" : null,
            }
            .Where(x => x is not null));

        var path = "message-streams/checkpoints" + (query.Length > 0 ? $"?{query}" : "");
        var response = await connection.HttpClient.GetAsync(path, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CheckpointDto>>(cancellationToken) ?? [];
    }

    public static async Task ResetCheckpoint(FlowlyConnection connection, string queueName, string consumerName, int? partition, long offset, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var response = await connection.HttpClient.PostAsJsonAsync(
            "message-streams/checkpoints/reset",
            new { QueueName = queueName, ConsumerName = consumerName, Partition = partition, Offset = offset },
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
    }

    public static async Task<IReadOnlyList<StreamPartitionDto>> ListStreamPartitions(
        FlowlyConnection connection, string? queue, string? consumer, int? partition, string? state, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var query = string.Join("&", new[]
            {
                queue is not null ? $"queue={Uri.EscapeDataString(queue)}" : null,
                consumer is not null ? $"consumer={Uri.EscapeDataString(consumer)}" : null,
                partition is not null ? $"partition={partition}" : null,
                state is not null ? $"state={Uri.EscapeDataString(state)}" : null,
            }
            .Where(x => x is not null));

        var path = "message-streams/partitions" + (query.Length > 0 ? $"?{query}" : "");
        var response = await connection.HttpClient.GetAsync(path, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<StreamPartitionDto>>(cancellationToken) ?? [];
    }

    public static async Task ResumeStreamPartition(FlowlyConnection connection, string queueName, string consumerName, int? partition, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var response = await connection.HttpClient.PostAsJsonAsync(
            "message-streams/partitions/resume",
            new { QueueName = queueName, ConsumerName = consumerName, Partition = partition },
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
    }

    public static async Task<IReadOnlyList<DeadLetterDto>> ListDeadLetters(
        FlowlyConnection connection, string? queue, string? subscription, string? status, string? messageId, int? skip, int? take, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var query = string.Join("&", new[]
            {
                queue is not null ? $"queue={Uri.EscapeDataString(queue)}" : null,
                subscription is not null ? $"subscription={Uri.EscapeDataString(subscription)}" : null,
                status is not null ? $"status={Uri.EscapeDataString(status)}" : null,
                messageId is not null ? $"messageId={Uri.EscapeDataString(messageId)}" : null,
                skip is not null ? $"skip={skip}" : null,
                take is not null ? $"take={take}" : null,
            }
            .Where(x => x is not null));

        var path = "dead-letters" + (query.Length > 0 ? $"?{query}" : "");
        var response = await connection.HttpClient.GetAsync(path, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<DeadLetterDto>>(cancellationToken) ?? [];
    }

    public static async Task RequeueDeadLetter(FlowlyConnection connection, string messageId, string? requeuedBy, CancellationToken cancellationToken, string? messageBody = null)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var response = await connection.HttpClient.PostAsJsonAsync(
            "dead-letters/requeue",
            new { MessageId = messageId, RequeuedBy = requeuedBy, MessageBody = messageBody },
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
    }

    public static async Task DiscardDeadLetter(FlowlyConnection connection, string messageId, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var response = await connection.HttpClient.PostAsJsonAsync(
            "dead-letters/discard",
            new { MessageId = messageId },
            cancellationToken);

        await EnsureSuccess(response, cancellationToken);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await ReadProblemDetail(response, cancellationToken);
        throw new HttpRequestException(
            detail is null ? $"The Flowly Management API answered {(int)response.StatusCode} ({response.ReasonPhrase})." : $"The Flowly Management API answered {(int)response.StatusCode}: {detail}",
            null,
            response.StatusCode);
    }

    private static async Task<string?> ReadProblemDetail(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

            return problem.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadTokenError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var error = body.RootElement.TryGetProperty("error", out var errorElement) ? errorElement.GetString() : null;
            var description = body.RootElement.TryGetProperty("error_description", out var descriptionElement) ? descriptionElement.GetString() : null;

            return error is null ? description : description is null ? error : $"{error}: {description}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task EnsureAuthorized(FlowlyConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.UsesOAuth2)
            return;

        if (connection.CachedAccessToken is not null && connection.TokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            return;

        var tokenEndpoint = connection.TokenEndpoint ?? $"{connection.Authority.TrimEnd('/')}/{Uri.EscapeDataString(connection.TenantId!)}/oauth2/v2.0/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = connection.ClientId!,
            ["client_secret"] = connection.ClientSecret!.ToPlainText(),
            ["scope"] = connection.Scope!,
        };

        using var tokenClient = connection.TokenMessageHandler is null ? new HttpClient() : new HttpClient(connection.TokenMessageHandler, false);
        var response = await tokenClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadTokenError(response, cancellationToken);

            throw new HttpRequestException(
                error is null
                    ? $"The token endpoint answered {(int)response.StatusCode} ({response.ReasonPhrase})."
                    : $"The token endpoint answered {(int)response.StatusCode}: {error}",
                null,
                response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The token endpoint returned an empty response.");

        connection.CachedAccessToken = token.AccessToken;
        connection.TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds);
        connection.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }
}
