using Flowly.MessageStreams.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Flowly.MessageStreams.Tests;

public class MessageStreamCheckpointAdminTests
{
    public class List
    {
        [Fact]
        public async Task WithNoRows_ReturnsEmpty()
        {
            using var fixture = new SqliteFixture();

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, null, null, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task FiltersByQueueNameAndConsumerName()
        {
            using var fixture = new SqliteFixture();
            await fixture.SeedRow("queue-a", "consumer-1", null, 1);
            await fixture.SeedRow("queue-a", "consumer-2", null, 2);
            await fixture.SeedRow("queue-b", "consumer-1", null, 3);

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, "queue-a", "consumer-1", CancellationToken.None);

            var info = Assert.Single(result);
            Assert.Equal("queue-a", info.QueueName);
            Assert.Equal("consumer-1", info.ConsumerName);
            Assert.Equal(1, info.Position);
        }

        [Fact]
        public async Task NonPartitionedSentinel_IsReportedAsNullPartition()
        {
            using var fixture = new SqliteFixture();
            await fixture.SeedRow("queue-a", "consumer-1", null, 5);

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, null, null, CancellationToken.None);

            Assert.Null(Assert.Single(result).Partition);
        }
    }

    public class Reset
    {
        [Fact]
        public async Task WhenRowExists_OverwritesPosition()
        {
            using var fixture = new SqliteFixture();
            await fixture.SeedRow("queue-a", "consumer-1", null, 1);

            await MessageStreamCheckpointAdmin.Reset(fixture.DbContextFactory, "queue-a", "consumer-1", null, 99, CancellationToken.None);

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, "queue-a", "consumer-1", CancellationToken.None);
            Assert.Equal(99, Assert.Single(result).Position);
        }

        [Fact]
        public async Task WhenRowDoesNotExist_CreatesIt()
        {
            using var fixture = new SqliteFixture();

            await MessageStreamCheckpointAdmin.Reset(fixture.DbContextFactory, "queue-a", "consumer-1", null, 7, CancellationToken.None);

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, "queue-a", "consumer-1", CancellationToken.None);
            Assert.Equal(7, Assert.Single(result).Position);
        }

        [Fact]
        public async Task WithPartition_TargetsOnlyThatPartition()
        {
            using var fixture = new SqliteFixture();
            await fixture.SeedRow("queue-a", "consumer-1", 0, 10);
            await fixture.SeedRow("queue-a", "consumer-1", 1, 20);

            await MessageStreamCheckpointAdmin.Reset(fixture.DbContextFactory, "queue-a", "consumer-1", 0, 999, CancellationToken.None);

            var result = await MessageStreamCheckpointAdmin.List(fixture.DbContextFactory, "queue-a", "consumer-1", CancellationToken.None);
            Assert.Equal(999, result.Single(x => x.Partition == 0).Position);
            Assert.Equal(20, result.Single(x => x.Partition == 1).Position);
        }
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;

        public IDbContextFactory<MessageStreamCheckpointDataContext> DbContextFactory { get; }

        public SqliteFixture()
        {
            var connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
            builder.UseSqlite(connectionString);
            DbContextFactory = new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);

            using var context = DbContextFactory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        public async Task SeedRow(string queueName, string consumerName, int? partition, long position)
        {
            await using var context = DbContextFactory.CreateDbContext();
            context.Checkpoints.Add(new MessageStreamCheckpointRecord
            {
                QueueName = queueName,
                ConsumerName = consumerName,
                Partition = partition ?? MessageStreamCheckpointRecord.NonPartitioned,
                Position = position,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        public void Dispose() => _keepAliveConnection.Dispose();
    }
}
