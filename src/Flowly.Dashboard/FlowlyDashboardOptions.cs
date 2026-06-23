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
}
