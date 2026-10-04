using System.Management.Automation;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>Lists stored message stream checkpoint positions via the Flowly Management API.</summary>
[Cmdlet(VerbsCommon.Get, "FlowlyMessageStreamCheckpoint")]
[OutputType(typeof(CheckpointDto))]
public sealed class GetFlowlyMessageStreamCheckpointCommand : PSCmdlet
{
    /// <summary>Filter results to this queue name.</summary>
    [Parameter]
    public string? QueueName { get; set; }

    /// <summary>Filter results to this consumer name.</summary>
    [Parameter]
    public string? ConsumerName { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var connection = FlowlySession.RequireCurrent();

        var checkpoints = FlowlyApiClient.ListCheckpoints(connection, QueueName, ConsumerName, CancellationToken.None)
            .GetAwaiter().GetResult();

        foreach (var checkpoint in checkpoints)
            WriteObject(checkpoint);
    }
}
