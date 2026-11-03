using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Discards a dead letter via the Flowly Management API: the dead letter is permanently deleted from the tracking store and the
///     message is not sent anywhere. A dead letter that has already been requeued cannot be discarded.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "FlowlyDeadLetter", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveFlowlyDeadLetterCommand : PSCmdlet
{
    /// <summary>The message id of the dead letter to discard.</summary>
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string MessageId { get; set; } = null!;

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        if (!ShouldProcess(MessageId, "Permanently discard the dead letter"))
            return;

        FlowlyApiClient.DiscardDeadLetter(connection, MessageId, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
}
