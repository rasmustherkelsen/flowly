namespace Flowly.Tests;

public class HandlerStepContextTests
{
    public class Constructor
    {
        [Fact]
        public void ExposesMessagePropertiesHandlerTypeAndCancellationToken()
        {
            var message = new OrderMessage();
            var properties = new MessageProperties("message-id", "correlation-id", RetryCount: 2, DeliveryCount: 3);
            using var cancellationTokenSource = new CancellationTokenSource();

            var handlerStepContext = new HandlerStepContext<OrderMessage>(message, properties, typeof(OrderHandler), cancellationTokenSource.Token);

            Assert.Same(message, handlerStepContext.Message);
            Assert.Same(properties, handlerStepContext.Properties);
            Assert.Equal(typeof(OrderHandler), handlerStepContext.HandlerType);
            Assert.Equal(cancellationTokenSource.Token, handlerStepContext.CancellationToken);
        }
    }

    private sealed class OrderMessage;

    private sealed class OrderHandler;
}
