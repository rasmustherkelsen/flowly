namespace Flowly.Tests;

public class SendStepContextTests
{
    public class Constructor
    {
        [Fact]
        public void ExposesMessageAndCancellationToken()
        {
            var message = new OrderMessage();
            using var cancellationTokenSource = new CancellationTokenSource();

            var sendStepContext = new SendStepContext<OrderMessage>(message, cancellationTokenSource.Token);

            Assert.Same(message, sendStepContext.Message);
            Assert.Equal(cancellationTokenSource.Token, sendStepContext.CancellationToken);
        }
    }

    private sealed class OrderMessage;
}
