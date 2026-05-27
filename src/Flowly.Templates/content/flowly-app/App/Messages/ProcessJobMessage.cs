using Flowly.Jobs;

namespace App.Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
