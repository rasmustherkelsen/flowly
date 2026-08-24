using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqPartitionedStreamTopologyValidatorTests
{
    public class Validate
    {
        [Fact]
        public async Task WhenActualPartitionCountMatchesDeclared_DoesNotThrow()
        {
            var pool = new FakeConnectionPool(("orders", ["orders-0", "orders-1", "orders-2"]));
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders", null, null, 3);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            await validator.Validate([new FakeQueueDescription("orders")], CancellationToken.None);
        }

        [Fact]
        public async Task WhenActualPartitionCountIsFewerThanDeclared_ThrowsWithBothCounts()
        {
            var pool = new FakeConnectionPool(("orders", ["orders-0", "orders-1"]));
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders", null, null, 3);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.Validate([new FakeQueueDescription("orders")], CancellationToken.None));

            Assert.Contains("orders", exception.Message);
            Assert.Contains("2 partition stream(s)", exception.Message);
            Assert.Contains("StreamPartitions(3)", exception.Message);
        }

        [Fact]
        public async Task WhenActualPartitionCountIsMoreThanDeclared_Throws()
        {
            var pool = new FakeConnectionPool(("orders", ["orders-0", "orders-1", "orders-2", "orders-3"]));
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders", null, null, 3);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            await Assert.ThrowsAsync<InvalidOperationException>(() => validator.Validate([new FakeQueueDescription("orders")], CancellationToken.None));
        }

        [Fact]
        public async Task WhenSuperStreamDoesNotExistOnBroker_ThrowsExplainingProvisioning()
        {
            var pool = new FakeConnectionPool(("orders", null));
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders", null, null, 3);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.Validate([new FakeQueueDescription("orders")], CancellationToken.None));

            Assert.Contains("does not exist on the broker", exception.Message);
            Assert.Contains("orders", exception.Message);
        }

        [Fact]
        public async Task NonPartitionedQueue_SkipsValidation()
        {
            var pool = new FakeConnectionPool();
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("telemetry", null, null);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            await validator.Validate([new FakeQueueDescription("telemetry")], CancellationToken.None);
        }

        [Fact]
        public async Task NonStreamQueue_SkipsValidation()
        {
            var pool = new FakeConnectionPool();
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool);

            await validator.Validate([new FakeQueueDescription("orders")], CancellationToken.None);
        }

        [Fact]
        public async Task WithEmptyQueueDescriptions_DoesNotThrow()
        {
            var pool = new FakeConnectionPool();
            var streamQueueManifest = new StreamQueueManifest();
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            await validator.Validate([], CancellationToken.None);
        }

        [Fact]
        public async Task MultiplePartitionedQueues_ValidatesEachIndependently()
        {
            var pool = new FakeConnectionPool(
                ("orders", ["orders-0", "orders-1"]),
                ("invoices", ["invoices-0"]));
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("orders", null, null, 2);
            streamQueueManifest.MarkAsStream("invoices", null, null, 5);
            var validator = new RabbitMqPartitionedStreamTopologyValidator("rabbitmq", pool, streamQueueManifest);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.Validate(
                [new FakeQueueDescription("orders"), new FakeQueueDescription("invoices")],
                CancellationToken.None));

            Assert.Contains("invoices", exception.Message);
        }
    }

    public class ProviderName
    {
        [Fact]
        public void ReturnsProviderNamePassedToConstructor()
        {
            var pool = new FakeConnectionPool();
            var validator = new RabbitMqPartitionedStreamTopologyValidator("my-rabbit", pool);

            Assert.Equal("my-rabbit", validator.ProviderName);
        }
    }

    private record FakeQueueDescription(string Name) : IQueueDescription
    {
        public TimeSpan DefaultMessageTimeToLive => TimeSpan.FromDays(1);
        public bool DeadLetterOnMessageExpiration => true;
        public TimeSpan LockDuration => TimeSpan.FromMinutes(5);
        public bool RequiresSession => false;
    }

    private class FakeConnectionPool(params (string SuperStreamName, string[]? Partitions)[] partitionsBySuperStream) : IRabbitMqConnectionPool
    {
        public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<StreamSystem> GetStreamSystem(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string[]> QueryStreamPartitions(string superStreamName, CancellationToken cancellationToken = default)
        {
            var match = partitionsBySuperStream.FirstOrDefault(x => x.SuperStreamName == superStreamName);

            if (match.Partitions is null)
                throw new QueryException($"query partitions failed code: {ResponseCode.StreamDoesNotExist}");

            return Task.FromResult(match.Partitions);
        }
    }
}
