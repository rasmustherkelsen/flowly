using Flowly.MessageStreams.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    public class SaveStreamPositionAsBootstrapSeed
    {
        [Fact]
        public async Task OverARowInitializedWithoutAPosition_StoresTheSeed()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(119, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task OfMinusOne_RoundTripsAndIsDistinguishableFromNoPosition()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, -1) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(-1, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task WithoutAnExistingRow_CreatesIt()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", 2, -1) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(-1, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", 2), CancellationToken.None));
        }

        [Fact]
        public async Task FollowedByABatchSave_ReplacesTheSeed()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, -1) { IsBootstrapSeed = true }, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 12), CancellationToken.None);

            Assert.Equal(12, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task OverAnExistingBatchPosition_DoesNotOverwriteIt()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 500), CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(500, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task OverAnExistingSeed_DoesNotOverwriteIt()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, -1) { IsBootstrapSeed = true }, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(-1, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task OverAnExistingPositionOfZero_DoesNotOverwriteIt()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicInitializeCheckpoint(context, CancellationToken.None);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 0), CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(0, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task WhenAnotherWriterInsertsAPositionBeforeTheSeedInserts_KeepsThatPosition()
        {
            using var fixture = new SqliteFixture();
            var competing = fixture.CreateCheckpoint();
            var checkpoint = fixture.CreateCheckpoint(beforeFirstSave: cancellationToken => competing.PublicSaveStreamPosition(
                new MessageStreamCheckpointSaveContext("my-consumer", null, 500), cancellationToken));

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(500, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", null), CancellationToken.None));
        }

        [Fact]
        public async Task WhenAnotherWriterInitializesTheRowBeforeTheSeedInserts_StoresTheSeed()
        {
            using var fixture = new SqliteFixture();
            var competing = fixture.CreateCheckpoint();
            var checkpoint = fixture.CreateCheckpoint(beforeFirstSave: cancellationToken => competing.PublicInitializeCheckpoint(
                new MessageStreamCheckpointContext("my-consumer", null), cancellationToken));

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(119, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", null), CancellationToken.None));
        }

        [Fact]
        public async Task WhenInsertingFailsAndNoRowExists_Rethrows()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint(beforeFirstSave: _ => throw new DbUpdateException("store unavailable"));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None));
        }

        [Fact]
        public async Task ForAnotherQueueWithTheSameConsumer_StoresItsOwnSeed()
        {
            using var fixture = new SqliteFixture();
            var otherQueue = fixture.CreateCheckpoint("other-queue");
            var checkpoint = fixture.CreateCheckpoint();
            await otherQueue.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 500), CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(119, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", null), CancellationToken.None));
            Assert.Equal(500, await otherQueue.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", null), CancellationToken.None));
        }

        [Fact]
        public async Task ABatchSaveAfterAnIgnoredSeed_StillOverwrites()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            var context = new MessageStreamCheckpointContext("my-consumer", null);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 500), CancellationToken.None);
            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 119) { IsBootstrapSeed = true }, CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", null, 3), CancellationToken.None);

            Assert.Equal(3, await checkpoint.PublicGetStreamPosition(context, CancellationToken.None));
        }

        [Fact]
        public async Task OnOnePartition_DoesNotAffectAnotherPartitionsRow()
        {
            using var fixture = new SqliteFixture();
            var checkpoint = fixture.CreateCheckpoint();
            await checkpoint.PublicInitializeCheckpoint(new MessageStreamCheckpointContext("my-consumer", 0), CancellationToken.None);
            await checkpoint.PublicInitializeCheckpoint(new MessageStreamCheckpointContext("my-consumer", 1), CancellationToken.None);

            await checkpoint.PublicSaveStreamPosition(new MessageStreamCheckpointSaveContext("my-consumer", 0, 29) { IsBootstrapSeed = true }, CancellationToken.None);

            Assert.Equal(29, await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", 0), CancellationToken.None));
            Assert.Null(await checkpoint.PublicGetStreamPosition(new MessageStreamCheckpointContext("my-consumer", 1), CancellationToken.None));
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

    private sealed class RunBeforeFirstSaveInterceptor(Func<CancellationToken, Task> beforeFirstSave) : SaveChangesInterceptor
    {
        private bool _ran;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_ran) return result;

            _ran = true;
            await beforeFirstSave(cancellationToken);

            return result;
        }
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqliteConnection _keepAliveConnection;
        private readonly IDbContextFactory<MessageStreamCheckpointDataContext> _dbContextFactory;

        public SqliteFixture()
        {
            _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
            _keepAliveConnection = new SqliteConnection(_connectionString);
            _keepAliveConnection.Open();

            _dbContextFactory = CreateFactory();

            using var context = _dbContextFactory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        public TestCheckpoint CreateCheckpoint(string queueName = "my-queue", Func<CancellationToken, Task>? beforeFirstSave = null)
            => new(beforeFirstSave is null ? _dbContextFactory : CreateFactory(new RunBeforeFirstSaveInterceptor(beforeFirstSave)), queueName);

        public void Dispose() => _keepAliveConnection.Dispose();

        private IDbContextFactory<MessageStreamCheckpointDataContext> CreateFactory(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
            builder.UseSqlite(_connectionString);

            if (interceptor is not null)
            {
                builder.AddInterceptors(interceptor);
            }

            return new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);
        }
    }
}
