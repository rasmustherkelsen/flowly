namespace Flowly.RabbitMQ.Tests;

public class RabbitMqConnectionLifetimeTests
{
    public class StopAsync
    {
        [Fact]
        public async Task DisposesTheMessageBusClientAndTheConnectionPool()
        {
            var connectionPool = new RabbitMqConnectionPool("amqp://guest:guest@localhost:5672/");
            var messageBusClient = new RabbitMqMessageBusClient(connectionPool);
            var rabbitMqConnectionLifetime = new RabbitMqConnectionLifetime(connectionPool, messageBusClient);

            await rabbitMqConnectionLifetime.StopAsync(CancellationToken.None);
        }
    }
}
