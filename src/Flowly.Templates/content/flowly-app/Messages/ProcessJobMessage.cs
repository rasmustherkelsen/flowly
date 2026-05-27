using Flowly.Jobs;

namespace Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
