using Flowly.MessageInfrastructure.Model;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class MessageContextTests
{
    public class Constructor
    {
        [Fact]
        public void AssignsMessageProperty()
        {
            var message = new SomeMessage("payload");

            var messageContext = new MessageContext<SomeMessage>(message, CancellationToken.None);

            Assert.Same(message, messageContext.Message);
        }

        [Fact]
        public void AssignsCancellationTokenProperty()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var messageContext = new MessageContext<SomeMessage>(new SomeMessage("x"), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, messageContext.CancellationToken);
        }

        [Fact]
        public void WithNullMessage_StoresNull()
        {
            var messageContext = new MessageContext<SomeMessage?>(null, CancellationToken.None);

            Assert.Null(messageContext.Message);
        }
    }

    private record SomeMessage(string Value);
}
