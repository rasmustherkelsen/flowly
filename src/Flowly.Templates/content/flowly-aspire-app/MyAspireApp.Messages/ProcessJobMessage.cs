using Flowly.Jobs;

namespace MyAspireApp.Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
