using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Requeues a dead letter via the Flowly Management API: the message is sent back to its original queue (or, for an event
///     subscription dead letter, addressed to the originating subscriber only) with a fresh set of retries, and the dead letter is
///     marked Requeued. Only a Pending dead letter can be requeued.
/// </summary>
[Cmdlet(VerbsLifecycle.Submit, "FlowlyDeadLetter", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class SubmitFlowlyDeadLetterCommand : PSCmdlet
{
    /// <summary>The message id of the dead letter to requeue.</summary>
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string MessageId { get; set; } = null!;

    /// <summary>
    ///     An optional identifier of the person or process requesting the requeue, stored as the audit trail of the dead letter. When omitted the API stores the authenticated
    ///     caller's identity instead: the principal's name for host authorization policies, the name, <c>sub</c> or <c>azp</c> claim for OAuth2, and nothing for an API key.
    /// </summary>
    [Parameter]
    public string? RequeuedBy { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        if (!ShouldProcess(MessageId, "Requeue the dead letter to its original queue"))
            return;

        FlowlyApiClient.RequeueDeadLetter(connection, MessageId, RequeuedBy, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
}
