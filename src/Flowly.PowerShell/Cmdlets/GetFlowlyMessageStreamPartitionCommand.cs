using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Lists the state of the message stream partitions owned by the instance the Flowly Management API answers from: Running,
///     Waiting (start position unresolved) or Halted (in-process retries exhausted), with the last error and the time of the last
///     state change. With several instances, each reports only its own partitions.
/// </summary>
[Cmdlet(VerbsCommon.Get, "FlowlyMessageStreamPartition")]
[OutputType(typeof(StreamPartitionDto))]
public sealed class GetFlowlyMessageStreamPartitionCommand : PSCmdlet
{
    /// <summary>Filter results to this queue name.</summary>
    [Parameter]
    public string? QueueName { get; set; }

    /// <summary>Filter results to this consumer name.</summary>
    [Parameter]
    public string? ConsumerName { get; set; }

    /// <summary>Filter results to this partition index.</summary>
    [Parameter]
    public int? Partition { get; set; }

    /// <summary>Filter results to this state: Running, Waiting or Halted.</summary>
    [Parameter]
    [ValidateSet("Running", "Waiting", "Halted", IgnoreCase = true)]
    public string? State { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        var partitions = FlowlyApiClient.ListStreamPartitions(connection, QueueName, ConsumerName, Partition, State, CancellationToken.None)
            .GetAwaiter().GetResult();

        foreach (var partition in partitions)
        {
            WriteObject(partition);
        }
    }
}
