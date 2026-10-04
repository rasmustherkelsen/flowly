using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Resets a message stream checkpoint's position via the Flowly Management API. The running handler only picks
///     this up on its next restart, and resetting to an earlier offset causes those messages to be reprocessed.
/// </summary>
[Cmdlet(VerbsCommon.Reset, "FlowlyMessageStreamCheckpoint", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class ResetFlowlyMessageStreamCheckpointCommand : PSCmdlet
{
    /// <summary>The queue name of the stream to reset.</summary>
    [Parameter(Mandatory = true)]
    public string QueueName { get; set; } = null!;

    /// <summary>The consumer name to reset.</summary>
    [Parameter(Mandatory = true)]
    public string ConsumerName { get; set; } = null!;

    /// <summary>The partition to reset. Omit for a non-partitioned stream.</summary>
    [Parameter]
    public int? Partition { get; set; }

    /// <summary>The offset to reset the position to.</summary>
    [Parameter(Mandatory = true)]
    public long Offset { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        var target = Partition.HasValue ? $"{QueueName}/{ConsumerName}[{Partition}]" : $"{QueueName}/{ConsumerName}";
        if (!ShouldProcess(target, $"Reset checkpoint to offset {Offset}"))
            return;

        FlowlyApiClient.ResetCheckpoint(connection, QueueName, ConsumerName, Partition, Offset, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
}
