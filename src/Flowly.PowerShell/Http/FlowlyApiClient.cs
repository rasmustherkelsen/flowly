using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CheckpointDto>>(cancellationToken) ?? [];
    }

    public static async Task ResetCheckpoint(FlowlyConnection connection, string queueName, string consumerName, int? partition, long offset, CancellationToken cancellationToken)
    {
        await EnsureAuthorized(connection, cancellationToken);

        var response = await connection.HttpClient.PostAsJsonAsync(
            "message-streams/checkpoints/reset",
            new { QueueName = queueName, ConsumerName = consumerName, Partition = partition, Offset = offset },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task EnsureAuthorized(FlowlyConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.UsesOAuth2)
            return;

        if (connection.CachedAccessToken is not null && connection.TokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            return;

        var tokenEndpoint = $"https://login.microsoftonline.com/{connection.TenantId}/oauth2/v2.0/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = connection.ClientId!,
            ["client_secret"] = connection.ClientSecret!,
            ["scope"] = connection.Scope!,
        };

        using var tokenClient = new HttpClient();
        var response = await tokenClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Entra ID token endpoint returned an empty response.");

        connection.CachedAccessToken = token.AccessToken;
        connection.TokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds);
        connection.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }
}
