using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Lists the dead letters tracked by the Flowly host the Management API answers from, newest first: message id, queue or topic,
///     subscription, body, properties, the broker's reason, status, the requeue audit, and for a message requeued with a corrected body <c>BodyReplaced</c> and the <c>ReplacementMessageBody</c> that was republished (the stored <c>MessageBody</c> stays unchanged). Requires dead letter tracking in the host.
/// </summary>
[Cmdlet(VerbsCommon.Get, "FlowlyDeadLetter")]
[OutputType(typeof(DeadLetterDto))]
public sealed class GetFlowlyDeadLetterCommand : PSCmdlet
{
    /// <summary>Filter results to this queue name (the topic name for an event subscription dead letter).</summary>
    [Parameter]
    public string? QueueName { get; set; }

    /// <summary>Filter results to this event subscription name.</summary>
    [Parameter]
    public string? SubscriptionName { get; set; }

    /// <summary>Filter results to this status: Pending or Requeued.</summary>
    [Parameter]
    [ValidateSet("Pending", "Requeued", IgnoreCase = true)]
    public string? Status { get; set; }

    /// <summary>Filter results to the dead letter with this message id.</summary>
    [Parameter]
    public string? MessageId { get; set; }

    /// <summary>Skip this many dead letters (newest first) before returning results.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int? Skip { get; set; }

    /// <summary>
    ///     Return at most this many dead letters (1 to 1000), taken from a single page of the Management API. Omit to return every dead
    ///     letter after <see cref="Skip" />: the cmdlet then reads the list in pages of 1000, because the API answers a request without
    ///     <c>take</c> with at most 100.
    /// </summary>
    [Parameter]
    [ValidateRange(1, 1000)]
    public int? First { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        var deadLetters = First is null
            ? FlowlyApiClient.ListAllDeadLetters(connection, QueueName, SubscriptionName, Status, MessageId, Skip, CancellationToken.None).GetAwaiter().GetResult()
            : FlowlyApiClient.ListDeadLetters(connection, QueueName, SubscriptionName, Status, MessageId, Skip, First, CancellationToken.None).GetAwaiter().GetResult();

        foreach (var deadLetter in deadLetters)
        {
            WriteObject(deadLetter);
        }
    }
}
