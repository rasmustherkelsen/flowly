namespace Flowly.PowerShell.Session;

/// <summary>Connection state established by <c>Connect-Flowly</c>, shared across cmdlets in the same session.</summary>
internal sealed class FlowlyConnection : IDisposable
{
    public required Uri BaseUri { get; init; }
    public required HttpClient HttpClient { get; init; }

    /// <summary>Non-null when authenticating via Entra ID client-credentials; used to acquire/refresh bearer tokens.</summary>
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Scope { get; init; }

    public string? CachedAccessToken { get; set; }
    public DateTimeOffset TokenExpiresAtUtc { get; set; }

    public bool UsesOAuth2 => TenantId is not null;

    public void Dispose() => HttpClient.Dispose();
}
