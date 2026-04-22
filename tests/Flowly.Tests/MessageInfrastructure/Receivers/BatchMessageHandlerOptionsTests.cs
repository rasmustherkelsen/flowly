namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class BatchMessageHandlerOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void AllPropertiesAreNull()
        {
            var batchMessageHandlerOptions = new BatchMessageHandlerOptions();

            Assert.Null(batchMessageHandlerOptions.MaxMessagesBeforeProcessing);
            Assert.Null(batchMessageHandlerOptions.MaxWaitTime);
        }
    }

    public class Setters
    {
        [Fact]
        public void StoresAllValues()
        {
            var batchMessageHandlerOptions = new BatchMessageHandlerOptions
            {
                MaxMessagesBeforeProcessing = 25,
                MaxWaitTime = TimeSpan.FromSeconds(30)
            };

            Assert.Equal(25, batchMessageHandlerOptions.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), batchMessageHandlerOptions.MaxWaitTime);
        }
    }
}