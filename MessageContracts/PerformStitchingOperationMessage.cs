using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Model;

namespace MessageContracts;

public record PerformStitchingOperationMessage(Guid ImportDefinitionId, string Description) : IJobMessage
{
    public string JobTypeName => "Stitching Job";
}
