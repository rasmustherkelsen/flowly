using Flowly.Jobs;

namespace MyAspireApp.App.Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
