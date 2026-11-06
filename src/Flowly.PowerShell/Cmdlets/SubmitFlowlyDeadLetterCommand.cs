using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Requeues a dead letter via the Flowly Management API: the message is sent back to its original queue (or, for an event
///     subscription dead letter, addressed to the originating subscriber only) with a fresh set of retries, and the dead letter is
///     marked Requeued. Only a Pending dead letter can be requeued. With <see cref="MessageBody" /> the message is republished with a corrected body instead of
///     the stored one; the stored body stays in the dead letter record, which then reports <c>BodyReplaced</c> and the replacement.
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

    /// <summary>
    ///     An optional replacement for the stored message body: the message is republished with this body instead, so a payload that made the handler fail can be corrected before
    ///     it is processed again. The stored body is kept in the dead letter record next to the replacement. Omit it to republish the stored body as it is. The API rejects (400) an empty
    ///     or whitespace value, a value larger than 256 KiB as UTF-8, and a value that is not valid JSON when the stored body is JSON.
    /// </summary>
    [Parameter]
    public string? MessageBody { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        if (!ShouldProcess(MessageId, "Requeue the dead letter to its original queue"))
            return;

        FlowlyApiClient.RequeueDeadLetter(connection, MessageId, RequeuedBy, CancellationToken.None, MessageBody)
            .GetAwaiter().GetResult();
    }
}
