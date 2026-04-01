using Flowly.Jobs.Model;

namespace MessageContracts;

public record ProcessOrder(Guid ImportDefinitionId, string Description) : IJobMessage
{
    public string JobTypeName => "Process Order";
}
