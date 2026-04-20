using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class ResolvedBatchMessageHandlerOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresBothProperties()
        {
            var resolvedBatchMessageHandlerOptions = new ResolvedBatchMessageHandlerOptions(
                MaxMessagesBeforeProcessing: 50,
                MaxWaitTime: TimeSpan.FromSeconds(10));

            Assert.Equal(50, resolvedBatchMessageHandlerOptions.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(10), resolvedBatchMessageHandlerOptions.MaxWaitTime);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new ResolvedBatchMessageHandlerOptions(10, TimeSpan.FromSeconds(5));
            var second = new ResolvedBatchMessageHandlerOptions(10, TimeSpan.FromSeconds(5));

            Assert.Equal(first, second);
        }
    }
}
