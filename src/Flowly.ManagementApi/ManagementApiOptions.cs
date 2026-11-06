namespace Flowly.ManagementApi;

internal enum ManagementApiAuthMode
{
    None,
    ApiKey,
    OAuth2,
    ExistingPolicies,
}

/// <summary>
///     Options for the Flowly Management API, configured via <c>services.AddFlowlyManagementApi(options => ...)</c>.
///     Exactly one authorization mode must be configured — <see cref="RequireApiKey(string)" />,
///     <see cref="UseOAuth2" />, or <see cref="RequireAuthorizationPolicies" /> — otherwise
///     <c>UseFlowlyManagementApi()</c> throws at startup rather than exposing an unauthenticated management surface.
/// </summary>
public sealed class ManagementApiOptions
{
    /// <summary>
    ///     The URL path prefix every action is mapped under, e.g. <c>{BasePath}/message-streams/checkpoints</c>.
    ///     Defaults to <c>/flowly/management</c> — always configurable, never hardcoded, since a host may already
    ///     own that segment or want to version it (<c>/flowly/management/v1</c>).
    /// </summary>
    public string BasePath { get; set; } = "/flowly/management";

    /// <summary>
    ///     Whether the generated OpenAPI discovery document itself requires the <see cref="ManagementCapability.Read" />
    ///     policy. Defaults to <see langword="true" /> — the document reveals exactly what management surface exists
    ///     on this deployment, a mild information-disclosure concern worth gating by default.
    /// </summary>
    public bool ProtectDiscoveryDocument { get; set; } = true;

    /// <summary>
    ///     When <see langword="true" />, also serves a minimal interactive HTML explorer at <c>{BasePath}/docs</c>,
    ///     purely for humans browsing the API by hand. Off by default — the whole premise of this API is "no UI
    ///     required."
    /// </summary>
    public bool EnableDocsUi { get; set; }

    internal ManagementApiAuthMode AuthMode { get; private set; } = ManagementApiAuthMode.None;
    internal string? ReadApiKey { get; private set; }
    internal string? WriteApiKey { get; private set; }
    internal ManagementApiOAuthOptions? OAuth { get; private set; }
    internal string? ReadPolicyName { get; private set; }
    internal string? WritePolicyName { get; private set; }

    /// <summary>
    ///     Protects every action with a single API key, presented via the <c>X-Flowly-Api-Key</c> request header.
    ///     Simplest option for a solo-operator scenario — grants both <see cref="ManagementCapability.Read" /> and
    ///     <see cref="ManagementCapability.Write" />.
    /// </summary>
    public void RequireApiKey(string apiKey)
    {
        ReadApiKey = apiKey;
        WriteApiKey = apiKey;
        AuthMode = ManagementApiAuthMode.ApiKey;
    }

    /// <summary>
    ///     Protects the API with two separate API keys — <paramref name="readApiKey" /> grants only
    ///     <see cref="ManagementCapability.Read" />; <paramref name="writeApiKey" /> grants both. A lightweight
    ///     read/write split without needing full OAuth2.
    /// </summary>
    public void RequireApiKey(string readApiKey, string writeApiKey)
    {
        ReadApiKey = readApiKey;
        WriteApiKey = writeApiKey;
        AuthMode = ManagementApiAuthMode.ApiKey;
    }

    /// <summary>
    ///     Protects the API with OAuth2 bearer tokens (e.g. an Entra ID app registration using the client-credentials
    ///     flow). See <see cref="ManagementApiOAuthOptions" /> for the App Role / scope matching rule. The bearer scheme
    ///     is registered under a Flowly-specific name and does not become the host's default authentication scheme.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="ManagementApiOAuthOptions.Authority" /> or <see cref="ManagementApiOAuthOptions.Audience" />
    ///     is empty after <paramref name="configure" /> ran.
    /// </exception>
    public void UseOAuth2(Action<ManagementApiOAuthOptions> configure)
    {
        var oauth = new ManagementApiOAuthOptions { Authority = "", Audience = "" };
        configure(oauth);

        if (string.IsNullOrWhiteSpace(oauth.Authority))
            throw new InvalidOperationException("Flowly Management API OAuth2 requires a non-empty Authority.");

        if (string.IsNullOrWhiteSpace(oauth.Audience))
            throw new InvalidOperationException("Flowly Management API OAuth2 requires a non-empty Audience.");

        OAuth = oauth;
        AuthMode = ManagementApiAuthMode.OAuth2;
    }

    /// <summary>
    ///     Defers entirely to authorization policies the host has already configured (e.g. via its own
    ///     <c>AddAuthorization</c> call), rather than Flowly wiring any authentication scheme of its own.
    /// </summary>
    /// <param name="readPolicyName">The policy name to require for <see cref="ManagementCapability.Read" /> actions.</param>
    /// <param name="writePolicyName">The policy name to require for <see cref="ManagementCapability.Write" /> actions.</param>
    public void RequireAuthorizationPolicies(string readPolicyName, string writePolicyName)
    {
        ReadPolicyName = readPolicyName;
        WritePolicyName = writePolicyName;
        AuthMode = ManagementApiAuthMode.ExistingPolicies;
    }
}
