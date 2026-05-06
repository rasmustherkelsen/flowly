using Flowly.MessageInfrastructure.Model;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class BatchMessageContextTests
{
    public class Constructor
    {
        [Fact]
        public void AssignsMessagesProperty()
        {
            var messages = new[] { new SomeMessage("a"), new SomeMessage("b") };

            var batchMessageContext = new BatchMessageContext<SomeMessage>(messages, CancellationToken.None);

            Assert.Same(messages, batchMessageContext.Messages);
        }

        [Fact]
        public void AssignsCancellationTokenProperty()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var batchMessageContext = new BatchMessageContext<SomeMessage>([], cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, batchMessageContext.CancellationToken);
        }

        [Fact]
        public void WithEmptyCollection_MessagesIsEmpty()
        {
            var batchMessageContext = new BatchMessageContext<SomeMessage>([], CancellationToken.None);

            Assert.Empty(batchMessageContext.Messages);
        }
    }

    private record SomeMessage(string Value);
}
