using Flowly.MessageStreams.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Flowly.MessageStreams.Tests;

public class EfCoreMessageStreamCheckpointTests
{
    public class GetStreamPosition
    {
        [Fact]
        public async Task BeforeAnyBatchProcessed_ReturnsNull()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);

            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);
            var position = await checkpoint.PublicGetStreamPosition(context, CancellationToken.None);

            Assert.Null(position);
        }

        [Fact]
        public async Task AfterSaveStreamPosition_ReturnsSavedOffset()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 42), CancellationToken.None);
            var position = await checkpoint.PublicGetStreamPosition(context, CancellationToken.None);

            Assert.Equal(42, position);
        }

        [Fact]
        public async Task SavedOffsetOfZero_IsDistinguishableFromNoRowAtAll()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 0), CancellationToken.None);
            var position = await checkpoint.PublicGetStreamPosition(context, CancellationToken.None);

            Assert.Equal(0, position);
        }

        [Fact]
        public async Task DifferentPartitionsOfSameConsumer_TrackIndependentPositions()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            await checkpoint.PublicInitializeCheckpoint(new MessageStreamCheckpointContext("my-consumer", 0), CancellationToken.None);
            await checkpoint.PublicInitializeCheckpoint(new MessageStreamCheckpointContext("my-consumer", 1), CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", 0, 10), CancellationToken.None);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", 1, 20), CancellationToken.None);

            Assert.Equal(10, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", 0), CancellationToken.None));
            Assert.Equal(20, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", 1), CancellationToken.None));
        }
    }

    public class SaveStreamPosition
    {
        [Fact]
        public async Task WithoutPriorInitializeCheckpoint_StillPersists()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 7), CancellationToken.None);
            var position = await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", null), CancellationToken.None);

            Assert.Equal(7, position);
        }

        [Fact]
        public async Task CalledTwice_OverwritesPreviousPosition()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 1), CancellationToken.None);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 2), CancellationToken.None);

            Assert.Equal(2, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }
    }

    private sealed class TestCheckpoint(IDbContextFactory<MessageStreamCheckpointDataContext> dbContextFactory, string queueName)
        : EfCoreMessageStreamCheckpoint<object>(dbContextFactory, queueName)
    {
        public Task PublicInitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken) =>
            InitializeCheckpoint(context, cancellationToken);

        public Task<long?> PublicGetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken) =>
            GetStreamPosition(context, cancellationToken);

        public Task PublicSaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken) =>
            SaveStreamPosition(context, cancellationToken);
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;
        private readonly IDbContextFactory<MessageStreamCheckpointDataContext> _dbContextFactory;

        public SqliteFixture()
        {
            var connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();

            var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
            builder.UseSqlite(connectionString);
            _dbContextFactory = new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);

            using var context = _dbContextFactory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        public TestCheckpoint CreateCheckpoint(string queueName = "my-queue") => new(_dbContextFactory, queueName);

        public void Dispose() => _keepAliveConnection.Dispose();
    }
}
