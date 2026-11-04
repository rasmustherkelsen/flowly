namespace Flowly.ManagementApi;

/// <summary>
///     Configures OAuth2 bearer-token authorization for the Flowly Management API — the standard shape for
///     protecting the API with an Entra ID (or any OIDC-compliant) app registration, including the client-credentials
///     flow (a script or CI job obtaining a token via client ID + client secret, with no interactive sign-in).
/// </summary>
public sealed class ManagementApiOAuthOptions
{
    /// <summary>
    ///     The token issuer authority, e.g. <c>https://login.microsoftonline.com/{tenant}/v2.0</c> for Entra ID.
    /// </summary>
    public required string Authority { get; set; }

    /// <summary>The expected audience (<c>aud</c> claim) on incoming tokens, e.g. <c>api://flowly-management</c>.</summary>
    public required string Audience { get; set; }

    /// <summary>
    ///     The claim value required for <see cref="ManagementCapability.Read" /> actions. Checked against both the
    ///     <c>roles</c> claim (Entra App Roles — what a client-credentials, app-only token carries) and the <c>scp</c>
    ///     claim (delegated OAuth scopes, a space-separated list matched per scope — what an interactive user sign-in
    ///     token carries), so the same configuration value works for either flow without the caller needing to know
    ///     which claim type their token uses. The JWT bearer handler maps an incoming <c>scp</c> claim to the type
    ///     <c>http://schemas.microsoft.com/identity/claims/scope</c> by default, and both claim types are matched.
    ///     Values are compared case-sensitively. Defaults to <c>"Flowly.Management.Read"</c>.
    /// </summary>
    public string ReadRequirement { get; set; } = "Flowly.Management.Read";

    /// <summary>
    ///     The claim value required for <see cref="ManagementCapability.Write" /> actions. See <see cref="ReadRequirement" />
    ///     for the App Role / scope matching rule. Defaults to <c>"Flowly.Management.Write"</c>.
    /// </summary>
    public string WriteRequirement { get; set; } = "Flowly.Management.Write";
}
