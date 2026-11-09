using System.Security;

namespace Flowly.PowerShell.Session;

/// <summary>Connection state established by <c>Connect-Flowly</c>, shared across cmdlets in the same session.</summary>
internal sealed class FlowlyConnection : IDisposable
{
    public const string DefaultAuthority = "https://login.microsoftonline.com";

    public required Uri BaseUri { get; init; }
    public required HttpClient HttpClient { get; init; }

    /// <summary>The Entra ID tenant; used to build the token endpoint when <see cref="TokenEndpoint" /> is not set.</summary>
    public string? TenantId { get; init; }

    /// <summary>Non-null when authenticating via OAuth2 client-credentials; used to acquire/refresh bearer tokens.</summary>
    public string? ClientId { get; init; }

    /// <summary>The client secret, held as a <see cref="SecureString" /> and revealed only while the token request is built.</summary>
    public SecureString? ClientSecret { get; init; }

    public string? Scope { get; init; }

    /// <summary>The base URL of the OAuth2 authority the token request is sent to, without tenant or path.</summary>
    public string Authority { get; init; } = DefaultAuthority;

    /// <summary>When set, the complete token endpoint URL, replacing <c>{Authority}/{TenantId}/oauth2/v2.0/token</c>.</summary>
    public string? TokenEndpoint { get; init; }

    public HttpMessageHandler? TokenMessageHandler { get; init; }

    public string? CachedAccessToken { get; set; }
    public DateTimeOffset TokenExpiresAtUtc { get; set; }

    public bool UsesOAuth2 => ClientId is not null;

    public void Dispose()
    {
        HttpClient.Dispose();
        ClientSecret?.Dispose();
    }
}
