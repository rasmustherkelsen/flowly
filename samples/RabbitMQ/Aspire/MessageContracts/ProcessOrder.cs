using Flowly.Jobs.Model;

namespace MessageContracts;

public record ProcessOrder(Guid OrderId, string Description) : IJobMessage
{
    public string JobTypeName => "Process Order";
}
