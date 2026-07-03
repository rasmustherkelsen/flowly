using Flowly.Dashboard.Auth;

namespace Flowly.Dashboard;

/// <summary>
///     Configuration options for the Flowly dashboard middleware.
/// </summary>
public sealed class FlowlyDashboardOptions
{
    /// <summary>
    ///     The URL path prefix under which the dashboard is mounted.
    ///     Defaults to <c>/flowly</c>. Must start with a forward slash and must not end with one.
    /// </summary>
    public string PathPrefix { get; set; } = "/flowly";

    /// <summary>
    ///     The display title shown in the dashboard UI header.
    ///     Defaults to <c>Flowly Dashboard</c>.
    /// </summary>
    public string Title { get; set; } = "Flowly Dashboard";

    /// <summary>
    ///     When set, enables OAuth2/OIDC authentication for the dashboard. All requests are
    ///     redirected to the configured identity provider until the user is authenticated.
    ///     When <see langword="null" /> (the default), the dashboard allows anonymous access.
    /// </summary>
    public OAuthAuthenticationOptions? Authentication { get; set; }
}
