using Flowly.Jobs;

namespace FlowlyMessageLib;

public record MyJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => nameof(MyJobMessage);
}
