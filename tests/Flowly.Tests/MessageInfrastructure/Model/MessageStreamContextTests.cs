using Flowly.MessageInfrastructure.Model;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class MessageStreamContextTests
{
    public class Constructor
    {
        [Fact]
        public void AssignsPartition()
        {
            var messageStreamContext = new MessageStreamContext<SomeMessage>([], CancellationToken.None, 3);

            Assert.Equal(3, messageStreamContext.Partition);
        }

        [Fact]
        public void AssignsPropertiesInMessageOrder()
        {
            var first = new MessageProperties("m1", "c") { CustomProperties = new Dictionary<string, string> { ["tenant"] = "a" } };
            var second = new MessageProperties("m2", "c") { CustomProperties = new Dictionary<string, string> { ["tenant"] = "b" } };

            var messageStreamContext = new MessageStreamContext<SomeMessage>([new SomeMessage("1"), new SomeMessage("2")], CancellationToken.None, null, [first, second]);

            Assert.Equal(["a", "b"], messageStreamContext.Properties.Select(properties => properties.CustomProperties["tenant"]));
        }

        [Fact]
        public void WithoutProperties_HasEmptyPropertiesPerMessage()
        {
            var messageStreamContext = new MessageStreamContext<SomeMessage>([new SomeMessage("1")], CancellationToken.None);

            Assert.Same(MessageProperties.Empty, Assert.Single(messageStreamContext.Properties));
        }
    }

    private record SomeMessage(string Value);
}
