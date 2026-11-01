using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Resumes a halted message stream partition via the Flowly Management API. The partition re-runs the batch it is holding with a
///     fresh set of in-process retries and continues from it; nothing is skipped, and when the batch fails again the partition halts
///     again. Works only against the instance that owns the partition and only for a Halted partition.
/// </summary>
[Cmdlet(VerbsLifecycle.Resume, "FlowlyMessageStreamPartition", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class ResumeFlowlyMessageStreamPartitionCommand : PSCmdlet
{
    /// <summary>The queue name of the stream the halted partition belongs to.</summary>
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string QueueName { get; set; } = null!;

    /// <summary>The consumer name of the handler reading the stream.</summary>
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string ConsumerName { get; set; } = null!;

    /// <summary>The partition to resume. Omit for a non-partitioned stream.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    public int? Partition { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        var target = Partition.HasValue ? $"{QueueName}/{ConsumerName}[{Partition}]" : $"{QueueName}/{ConsumerName}";
        if (!ShouldProcess(target, "Resume the halted partition and re-run its held batch"))
            return;

        FlowlyApiClient.ResumeStreamPartition(connection, QueueName, ConsumerName, Partition, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
}
