using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Flowly.PowerShell.Cmdlets;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Tests.Fakes;

internal sealed class CmdletHost : IDisposable
{
    private readonly System.Management.Automation.PowerShell _powerShell;

    public CmdletHost(HttpMessageHandler handler)
    {
        FlowlySession.Current = ApiKeyConnection.Create(handler);

        var initialSessionState = InitialSessionState.CreateDefault2();
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Connect-Flowly", typeof(ConnectFlowlyCommand), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-FlowlyMessageStreamPartition", typeof(GetFlowlyMessageStreamPartitionCommand), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Resume-FlowlyMessageStreamPartition", typeof(ResumeFlowlyMessageStreamPartitionCommand), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-FlowlyDeadLetter", typeof(GetFlowlyDeadLetterCommand), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Submit-FlowlyDeadLetter", typeof(SubmitFlowlyDeadLetterCommand), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Remove-FlowlyDeadLetter", typeof(RemoveFlowlyDeadLetterCommand), null));

        _powerShell = System.Management.Automation.PowerShell.Create(initialSessionState);
    }

    public Collection<PSObject> Invoke(string commandName, IDictionary<string, object?>? parameters = null)
    {
        _powerShell.Commands.Clear();
        _powerShell.AddCommand(commandName);

        foreach (var parameter in parameters ?? new Dictionary<string, object?>())
        {
            _powerShell.AddParameter(parameter.Key, parameter.Value);
        }

        return _powerShell.Invoke();
    }

    public Collection<PSObject> InvokePipeline(string producer, string consumer, IDictionary<string, object?> consumerParameters)
    {
        _powerShell.Commands.Clear();
        _powerShell.AddCommand(producer).AddCommand(consumer);

        foreach (var parameter in consumerParameters)
        {
            _powerShell.AddParameter(parameter.Key, parameter.Value);
        }

        return _powerShell.Invoke();
    }

    public void Dispose()
    {
        _powerShell.Dispose();
        FlowlySession.Current?.Dispose();
        FlowlySession.Current = null;
    }
}
