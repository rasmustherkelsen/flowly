namespace Flowly.RabbitMQ.Tests;

public class RabbitMqLazyConnectionTests
{
    public class DisposeAsync
    {
        [Fact]
        public async Task WhenNeverConnected_DoesNotThrow()
        {
            var rabbitMqLazyConnection = new RabbitMqLazyConnection("amqp://guest:guest@localhost:5672/", "test-connection");

            await rabbitMqLazyConnection.DisposeAsync();
        }
    }
}
