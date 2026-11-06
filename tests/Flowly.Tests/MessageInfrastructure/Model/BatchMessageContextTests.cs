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
        public void AssignsPropertiesInMessageOrder()
        {
            var first = new MessageProperties("m1", "c") { CustomProperties = new Dictionary<string, string> { ["tenant"] = "a" } };
            var second = new MessageProperties("m2", "c") { CustomProperties = new Dictionary<string, string> { ["tenant"] = "b" } };

            var batchMessageContext = new BatchMessageContext<SomeMessage>([new SomeMessage("1"), new SomeMessage("2")], CancellationToken.None, [first, second]);

            Assert.Equal(["a", "b"], batchMessageContext.Properties.Select(properties => properties.CustomProperties["tenant"]));
        }

        [Fact]
        public void WithoutProperties_HasEmptyPropertiesPerMessage()
        {
            var batchMessageContext = new BatchMessageContext<SomeMessage>([new SomeMessage("1"), new SomeMessage("2")], CancellationToken.None);

            Assert.Equal(2, batchMessageContext.Properties.Count);
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
