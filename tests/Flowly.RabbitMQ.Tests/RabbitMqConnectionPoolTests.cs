namespace Flowly.RabbitMQ.Tests;

public class RabbitMqConnectionPoolTests
{
    public class ResolveStreamEndpoint
    {
        [Fact]
        public void WithDefaultConstruction_ResolvesPort5552()
        {
            var connectionPool = new RabbitMqConnectionPool("amqp://guest:guest@localhost:5672/");

            var endpoint = connectionPool.ResolveStreamEndpoint();

            Assert.Equal(5552, endpoint.Port);
        }

        [Fact]
        public void WithExplicitStreamPort_ResolvesThatPort()
        {
            var connectionPool = new RabbitMqConnectionPool("amqp://guest:guest@localhost:5672/", 15552);

            var endpoint = connectionPool.ResolveStreamEndpoint();

            Assert.Equal(15552, endpoint.Port);
        }

        [Fact]
        public void ResolvesHostFromUri_IndependentOfAmqpPort()
        {
            var connectionPool = new RabbitMqConnectionPool("amqp://user:pass@broker-host:5672/vhost");

            var endpoint = connectionPool.ResolveStreamEndpoint();

            Assert.Equal("broker-host", endpoint.Host);
        }
    }

    public class ResolveClientProvidedName
    {
        [Fact]
        public void ReturnsNameEndingWithGivenRole()
        {
            var clientProvidedName = RabbitMqConnectionPool.ResolveClientProvidedName("publisher");

            Assert.EndsWith("-publisher", clientProvidedName);
        }

        [Fact]
        public void PublisherAndConsumerRolesResolveToDistinctNames()
        {
            var publisherName = RabbitMqConnectionPool.ResolveClientProvidedName("publisher");
            var consumerName = RabbitMqConnectionPool.ResolveClientProvidedName("consumer");

            Assert.NotEqual(publisherName, consumerName);
        }

        [Fact]
        public void NeverReturnsNullOrEmpty()
        {
            var clientProvidedName = RabbitMqConnectionPool.ResolveClientProvidedName("consumer");

            Assert.False(string.IsNullOrEmpty(clientProvidedName));
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task WhenNeverConnected_DoesNotThrow()
        {
            var connectionPool = new RabbitMqConnectionPool("amqp://guest:guest@localhost:5672/");

            await connectionPool.DisposeAsync();
        }
    }
}
