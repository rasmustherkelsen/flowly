namespace Flowly.PowerShell.Session;

/// <summary>Module-scoped session state — the single active connection established by <c>Connect-Flowly</c>.</summary>
internal static class FlowlySession
{
    public static FlowlyConnection? Current { get; set; }

    public static FlowlyConnection RequireCurrent() =>
        Current ?? throw new InvalidOperationException("Not connected. Run Connect-Flowly first.");
}
