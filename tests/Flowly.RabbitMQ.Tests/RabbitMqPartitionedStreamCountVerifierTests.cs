using RabbitMQ.Client;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqPartitionedStreamCountVerifierTests
{
    public class Verify
    {
        [Fact]
        public async Task WhenBrokerCountMatchesDeclared_DoesNotThrow()
        {
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(new FakeConnectionPool(["orders-0", "orders-1", "orders-2"]));

            await rabbitMqPartitionedStreamCountVerifier.Verify("orders", 3, CancellationToken.None);
        }

        [Fact]
        public async Task WhenBrokerHasFewerThanDeclared_ThrowsNamingStreamAndBothCounts()
        {
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(new FakeConnectionPool(["orders-0", "orders-1"]));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamCountVerifier.Verify("orders", 3, CancellationToken.None));

            Assert.Contains("'orders'", exception.Message);
            Assert.Contains("2 partition stream(s)", exception.Message);
            Assert.Contains("StreamPartitions(3)", exception.Message);
        }

        [Fact]
        public async Task WhenBrokerHasMoreThanDeclared_ThrowsNamingStreamAndBothCounts()
        {
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(new FakeConnectionPool(["orders-0", "orders-1", "orders-2", "orders-3"]));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamCountVerifier.Verify("orders", 2, CancellationToken.None));

            Assert.Contains("'orders'", exception.Message);
            Assert.Contains("4 partition stream(s)", exception.Message);
            Assert.Contains("StreamPartitions(2)", exception.Message);
        }

        [Fact]
        public async Task WhenMismatched_MessageStatesFlowlyDoesNotReconcile()
        {
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(new FakeConnectionPool(["orders-0"]));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamCountVerifier.Verify("orders", 2, CancellationToken.None));

            Assert.Contains("never deletes or re-creates", exception.Message);
        }

        [Fact]
        public async Task WhenSuperStreamDoesNotExist_PropagatesQueryException()
        {
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(new FakeConnectionPool(null));

            await Assert.ThrowsAsync<QueryException>(() => rabbitMqPartitionedStreamCountVerifier.Verify("orders", 2, CancellationToken.None));
        }

        [Fact]
        public async Task PassesCancellationTokenToBrokerQuery()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var pool = new FakeConnectionPool(["orders-0"]);
            var rabbitMqPartitionedStreamCountVerifier = new RabbitMqPartitionedStreamCountVerifier(pool);

            await rabbitMqPartitionedStreamCountVerifier.Verify("orders", 1, cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, pool.ReceivedCancellationToken);
        }
    }

    private class FakeConnectionPool(string[]? partitions) : IRabbitMqConnectionPool
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StreamSystem> GetStreamSystem(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<string[]> QueryStreamPartitions(string superStreamName, CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;

            if (partitions is null)
                throw new QueryException($"query partitions failed code: {ResponseCode.StreamDoesNotExist}");

            return Task.FromResult(partitions);
        }
    }
}
