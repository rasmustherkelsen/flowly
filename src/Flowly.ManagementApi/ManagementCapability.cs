namespace Flowly.ManagementApi;

/// <summary>
///     The authorization tier a <see cref="IFlowlyManagementAction" /> requires. Mirrors the Dashboard's
///     viewer/submitter split at a coarser, two-tier granularity — enough to distinguish read-only actions from
///     mutating ones without a scope-per-endpoint permission matrix.
/// </summary>
public enum ManagementCapability
{
    /// <summary>A read-only action with no side effects (e.g. listing state).</summary>
    Read,

    /// <summary>A mutating action (e.g. resetting a stored position).</summary>
    Write,
}
