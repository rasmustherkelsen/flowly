namespace Flowly.Dashboard.Auth;

/// <summary>
///     Configures OAuth2/OIDC authentication for the Flowly Dashboard using the Authorization Code flow with PKCE.
/// </summary>
/// <remarks>
///     <para>
///         The dashboard registers its own isolated cookie and OIDC authentication schemes
///         (<c>FlowlyDashboard.Cookies</c> / <c>FlowlyDashboard.Oidc</c>) so that it does not
///         interfere with any authentication the host application has already configured.
///     </para>
///     <para>
///         The OAuth provider must have the following redirect URI registered:
///         <c>{baseUrl}{pathPrefix}/signin-oidc</c> (e.g. <c>https://myapp.com/flowly/signin-oidc</c>).
///         If the provider enforces an allowed post-logout redirect list, also register
///         <c>{baseUrl}{pathPrefix}/</c>.
///     </para>
///     <para>
///         <b>Client secret requirement:</b> whether a client secret is needed depends on how the app is
///         registered with the provider. For <b>Azure Entra ID</b>, registering as a <em>Web</em> platform
///         (confidential client) requires a secret — the server exchanges the authorization code for tokens
///         server-to-server, and Entra ID will reject the request without one. The secret is used exclusively
///         in that server-to-server call and is never transmitted to the browser.
///         Generate a secret under <em>Certificates &amp; secrets → Client secrets</em> and store it
///         securely (user secrets, environment variable, or Azure Key Vault) — never in source control.
///         For <b>Google</b> and other providers that support public clients, the secret can be omitted
///         when the provider is configured accordingly.
///     </para>
///     <para>
///         <b>Azure Entra ID</b> — Register an application under <em>Azure Active Directory → App registrations</em>.
///         Add a <em>Web</em> platform redirect URI (not SPA — the code exchange happens server-side).
///         Set <see cref="Authority" /> to <c>https://login.microsoftonline.com/{tenantId}/v2.0</c>
///         (use <c>common</c> for multi-tenant). Generate a client secret and pass it as
///         <see cref="ClientSecret" />. Assign app roles via the manifest and grant them to users
///         under <em>Enterprise applications</em>. Example:
///         <code>
///             options.Authentication = new OAuthAuthenticationOptions(
///                 clientId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
///                 authority: "https://login.microsoftonline.com/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/v2.0",
///                 clientSecret: configuration["FlowlyDashboard:ClientSecret"]);
///         </code>
///     </para>
///     <para>
///         <b>Google</b> — Create an OAuth 2.0 Client ID under <em>APIs &amp; Services → Credentials</em>.
///         The authority is always <c>https://accounts.google.com</c>.
///         Google does not emit role claims; use <see cref="ViewerPolicies" /> / <see cref="SubmitterPolicies" />
///         pointing to custom ASP.NET Core policies that check <c>sub</c> or <c>email</c> claims instead.
///         Example:
///         <code>
///             options.Authentication = new OAuthAuthenticationOptions(
///                 clientId: "xxxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com",
///                 authority: "https://accounts.google.com");
///         </code>
///     </para>
/// </remarks>
public sealed class OAuthAuthenticationOptions
{
    /// <summary>Gets the OAuth2 client identifier registered with the identity provider.</summary>
    public string ClientId { get; }

    /// <summary>
    ///     Gets the OIDC issuer / authority URL used for OpenID Connect discovery.
    ///     Examples: <c>https://login.microsoftonline.com/{tenant}/v2.0</c> (Entra ID),
    ///     <c>https://accounts.google.com</c> (Google).
    /// </summary>
    public string Authority { get; }

    /// <summary>
    ///     Gets the client secret used during the server-side authorization code exchange.
    ///     Required when the identity provider is configured as a confidential client (e.g. Azure Entra ID
    ///     with a <em>Web</em> platform registration). The secret is sent directly from the server to the
    ///     provider's token endpoint and is never transmitted to the browser.
    ///     Store it securely — user secrets, an environment variable, or a secrets manager —
    ///     never in source control. Pass <see langword="null" /> for providers that support public clients.
    /// </summary>
    public string? ClientSecret { get; }

    /// <summary>
    ///     Gets the roles that grant view-only access to the dashboard.
    ///     A user satisfying <b>any</b> role in this list is granted viewer access.
    ///     When <see langword="null" />, all authenticated users are viewers.
    /// </summary>
    public IReadOnlyList<string>? ViewerRoles { get; init; }

    /// <summary>
    ///     Gets the names of ASP.NET Core authorization policies that grant view-only
    ///     access to the dashboard. A user satisfying <b>any</b> policy in this list is granted
    ///     viewer access. When <see langword="null" />, all authenticated users are viewers.
    ///     The named policies must be registered before <c>UseFlowlyDashboard</c> is called.
    /// </summary>
    public IReadOnlyList<string>? ViewerPolicies { get; init; }

    /// <summary>
    ///     Gets the roles that grant submit (mutation) access to the dashboard.
    ///     A user satisfying <b>any</b> role in this list may submit messages and trigger jobs.
    ///     When <see langword="null" />, the viewer restriction also governs submit access
    ///     (i.e. all viewers can submit).
    /// </summary>
    public IReadOnlyList<string>? SubmitterRoles { get; init; }

    /// <summary>
    ///     Gets the names of ASP.NET Core authorization policies that grant submit
    ///     (mutation) access to the dashboard. A user satisfying <b>any</b> policy in this list
    ///     may submit messages and trigger jobs.
    ///     When <see langword="null" />, the viewer restriction also governs submit access.
    ///     The named policies must be registered before <c>UseFlowlyDashboard</c> is called.
    /// </summary>
    public IReadOnlyList<string>? SubmitterPolicies { get; init; }

    /// <param name="clientId">The OAuth2 client identifier registered with the identity provider.</param>
    /// <param name="authority">
    ///     The OIDC issuer URL (e.g. <c>https://login.microsoftonline.com/{tenant}/v2.0</c>
    ///     for Entra ID or <c>https://accounts.google.com</c> for Google).
    /// </param>
    /// <param name="clientSecret">
    ///     The client secret used during the server-side authorization code exchange.
    ///     Required for confidential client registrations such as Azure Entra ID with a <em>Web</em> platform.
    ///     The secret is sent server-to-server to the token endpoint and never reaches the browser.
    ///     Store it securely — user secrets, an environment variable, or a secrets manager —
    ///     and never in source control. Pass <see langword="null" /> for providers that support public clients.
    /// </param>
    /// <param name="viewerRoles">
    ///     Roles that grant view-only access. <see langword="null" /> allows all authenticated users to view.
    /// </param>
    /// <param name="viewerPolicies">
    ///     Named policies that grant view-only access. <see langword="null" /> allows all authenticated users to view.
    /// </param>
    /// <param name="submitterRoles">
    ///     Roles that grant submit access. <see langword="null" /> falls back to the viewer restriction.
    /// </param>
    /// <param name="submitterPolicies">
    ///     Named policies that grant submit access. <see langword="null" /> falls back to the viewer restriction.
    /// </param>
    public OAuthAuthenticationOptions(
        string clientId,
        string authority,
        string? clientSecret = null,
        IReadOnlyList<string>? viewerRoles    = null,
        IReadOnlyList<string>? viewerPolicies = null,
        IReadOnlyList<string>? submitterRoles    = null,
        IReadOnlyList<string>? submitterPolicies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);

        ClientId          = clientId;
        Authority         = authority;
        ClientSecret      = clientSecret;
        ViewerRoles       = viewerRoles;
        ViewerPolicies    = viewerPolicies;
        SubmitterRoles    = submitterRoles;
        SubmitterPolicies = submitterPolicies;
    }
}
