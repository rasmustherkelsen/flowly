using System.Management.Automation;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>Clears the connection established by <c>Connect-Flowly</c>.</summary>
[Cmdlet(VerbsCommunications.Disconnect, "Flowly")]
public sealed class DisconnectFlowlyCommand : PSCmdlet
{
    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        FlowlySession.Current?.Dispose();
        FlowlySession.Current = null;
    }
}
