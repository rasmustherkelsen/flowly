using Flowly.Jobs;

namespace Api.Messages;

internal record ProcessOrder(Guid OrderId, string Description) : IJobMessage
{
    public string JobTypeName => "Process Order";
}
