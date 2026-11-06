using Flowly.Jobs.Messages;
using Flowly.MessageInfrastructure.SendSteps;

namespace Flowly.Jobs.Tests.Messages;

public class FlowlysysMessageConventionTests
{
    public class AllFlowlysysTypes
    {
        [Fact]
        public void ImplementIFlowlySystemMessage()
        {
            var flowlysysTypes = typeof(FlowlysysJobFailedMessage).Assembly
                .GetTypes()
                .Where(type => type.Name.StartsWith("Flowlysys", StringComparison.Ordinal))
                .ToArray();

            var typesMissingTheMarker = flowlysysTypes
                .Where(type => !typeof(IFlowlySystemMessage).IsAssignableFrom(type))
                .Select(type => type.FullName)
                .ToArray();

            Assert.NotEmpty(flowlysysTypes);
            Assert.Empty(typesMissingTheMarker);
        }
    }
}
