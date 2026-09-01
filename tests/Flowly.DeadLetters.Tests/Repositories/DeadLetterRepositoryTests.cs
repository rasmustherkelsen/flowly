using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Tests.Fakes;
using Flowly.DeadLetters.Tests.Helpers;
using Flowly.Transport;
using Microsoft.EntityFrameworkCore;

namespace Flowly.DeadLetters.Tests.Repositories;

public class DeadLetterRepositoryTests
{
    private static DateTimeOffset TruncateToMillisecond(DateTimeOffset value)
        => new(value.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, value.Offset);

    public class SaveBatch
    {
        [Fact]
        public async Task PersistsNewMessages()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1", RawBody = "hello" }], "orders");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Equal("orders", deadLetter.QueueName);
            Assert.Equal("hello", deadLetter.MessageBody);
            Assert.Null(deadLetter.SubscriptionName);
        }

        [Fact]
        public async Task SkipsMessagesThatAlreadyExist()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }, new FakeDeadLetterMessage { MessageId = "msg-2" }], "orders");

            await using var context = await factory.CreateDbContextAsync();
            var count = await context.DeadLetters.CountAsync(d => d.MessageId == "msg-1");
            Assert.Equal(1, count);
            Assert.True(await context.DeadLetters.AnyAsync(d => d.MessageId == "msg-2"));
        }

        [Fact]
        public async Task DeduplicatesWithinTheSameBatch()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }, new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            await using var context = await factory.CreateDbContextAsync();
            var count = await context.DeadLetters.CountAsync(d => d.MessageId == "msg-1");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task StoresApplicationPropertiesAsJson()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var message = new FakeDeadLetterMessage
            {
                MessageId = "msg-1",
                ApplicationProperties = new Dictionary<string, object> { ["source"] = "orders-api" }
            };

            await repository.SaveBatch([message], "orders");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Contains("orders-api", deadLetter.MessageProperties);
        }

        [Fact]
        public async Task StoresDeadLetterReasonAndErrorDescription()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var message = new FakeDeadLetterMessage
            {
                MessageId = "msg-1",
                DeadLetterReason = "MaxDeliveryCountExceeded",
                DeadLetterErrorDescription = "Exceeded max delivery count"
            };

            await repository.SaveBatch([message], "orders");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Equal("MaxDeliveryCountExceeded", deadLetter.DeadLetterReason);
            Assert.Equal("Exceeded max delivery count", deadLetter.DeadLetterErrorDescription);
        }

        [Fact]
        public async Task WithEmptyBatch_DoesNotThrow()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            await repository.SaveBatch([], "orders");

            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(0, await context.DeadLetters.CountAsync());
        }
    }

    public class SaveBatchForSubscription
    {
        [Fact]
        public async Task PersistsMessagesWithSubscriptionName()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            await repository.SaveBatchForSubscription([new FakeDeadLetterMessage { MessageId = "msg-1" }], "order-placed", "notification-handler");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Equal("order-placed", deadLetter.QueueName);
            Assert.Equal("notification-handler", deadLetter.SubscriptionName);
        }
    }

    public class GetLastIngestionTime
    {
        [Fact]
        public async Task WithNoMessages_ReturnsNull()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var result = await repository.GetLastIngestionTime("orders");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsMostRecentDeadLetteredAt()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var older = DateTimeOffset.UtcNow.AddHours(-2);
            var newer = TruncateToMillisecond(DateTimeOffset.UtcNow.AddHours(-1));
            await repository.SaveBatch([
                new FakeDeadLetterMessage { MessageId = "msg-1", EnqueuedTime = older },
                new FakeDeadLetterMessage { MessageId = "msg-2", EnqueuedTime = newer }
            ], "orders");

            var result = await repository.GetLastIngestionTime("orders");

            Assert.Equal(newer, result);
        }

        [Fact]
        public async Task IgnoresMessagesFromEventSubscriptions()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatchForSubscription([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders", "some-subscriber");

            var result = await repository.GetLastIngestionTime("orders");

            Assert.Null(result);
        }

        [Fact]
        public async Task IgnoresMessagesFromOtherQueues()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "payments");

            var result = await repository.GetLastIngestionTime("orders");

            Assert.Null(result);
        }
    }

    public class GetLastIngestionTimeForSubscription
    {
        [Fact]
        public async Task WithNoMessages_ReturnsNull()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var result = await repository.GetLastIngestionTimeForSubscription("order-placed", "notification-handler");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsMostRecentDeadLetteredAtForMatchingSubscription()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var enqueuedTime = TruncateToMillisecond(DateTimeOffset.UtcNow.AddHours(-1));
            await repository.SaveBatchForSubscription([new FakeDeadLetterMessage { MessageId = "msg-1", EnqueuedTime = enqueuedTime }], "order-placed", "notification-handler");
            await repository.SaveBatchForSubscription([new FakeDeadLetterMessage { MessageId = "msg-2" }], "order-placed", "audit-log-handler");

            var result = await repository.GetLastIngestionTimeForSubscription("order-placed", "notification-handler");

            Assert.Equal(enqueuedTime, result);
        }
    }

    public class GetAll
    {
        [Fact]
        public async Task WithNoMessages_ReturnsEmptyCollection()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var result = await repository.GetAll();

            Assert.Empty(result);
        }

        [Fact]
        public async Task ReturnsAllMessagesOrderedByMostRecentFirst()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var older = DateTimeOffset.UtcNow.AddHours(-2);
            var newer = DateTimeOffset.UtcNow.AddHours(-1);
            await repository.SaveBatch([
                new FakeDeadLetterMessage { MessageId = "msg-old", EnqueuedTime = older },
                new FakeDeadLetterMessage { MessageId = "msg-new", EnqueuedTime = newer }
            ], "orders");

            var result = await repository.GetAll();

            Assert.Equal(["msg-new", "msg-old"], result.Select(d => d.MessageId));
        }
    }

    public class Get
    {
        [Fact]
        public async Task WithExistingMessageId_ReturnsDeadLetter()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            var result = await repository.Get("msg-1");

            Assert.NotNull(result);
            Assert.Equal("msg-1", result.MessageId);
        }

        [Fact]
        public async Task WithUnknownMessageId_ReturnsNull()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var result = await repository.Get("nonexistent-id");

            Assert.Null(result);
        }
    }

    public class MarkAsRequeued
    {
        [Fact]
        public async Task UpdatesStatusAndRequeuedFields()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            await repository.MarkAsRequeued("msg-1", "admin");

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
            Assert.Equal("admin", deadLetter.RequeuedBy);
            Assert.NotNull(deadLetter.RequeuedAt);
        }

        [Fact]
        public async Task WithNullRequeuedBy_StoresNull()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            await repository.MarkAsRequeued("msg-1", null);

            await using var context = await factory.CreateDbContextAsync();
            var deadLetter = await context.DeadLetters.SingleAsync(d => d.MessageId == "msg-1");
            Assert.Null(deadLetter.RequeuedBy);
        }
    }

    public class Delete
    {
        [Fact]
        public async Task RemovesTheMessage()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }], "orders");

            await repository.Delete("msg-1");

            await using var context = await factory.CreateDbContextAsync();
            Assert.False(await context.DeadLetters.AnyAsync(d => d.MessageId == "msg-1"));
        }

        [Fact]
        public async Task WithUnknownMessageId_DoesNotThrow()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            await repository.Delete("nonexistent-id");
        }
    }

    public class DeleteRequeuedOlderThan
    {
        [Fact]
        public async Task DeletesOnlyRequeuedMessagesOlderThanAge()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "old-requeued" }, new FakeDeadLetterMessage { MessageId = "new-requeued" }, new FakeDeadLetterMessage { MessageId = "still-pending" }], "orders");
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.DeadLetters
                    .Where(d => d.MessageId == "old-requeued")
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DeadLetterStatus.Requeued).SetProperty(d => d.RequeuedAt, DateTimeOffset.UtcNow.AddDays(-10)));
                await context.DeadLetters
                    .Where(d => d.MessageId == "new-requeued")
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DeadLetterStatus.Requeued).SetProperty(d => d.RequeuedAt, DateTimeOffset.UtcNow));
            }

            var deletedCount = await repository.DeleteRequeuedOlderThan(TimeSpan.FromDays(7));

            Assert.Equal(1, deletedCount);
            await using var verifyContext = await factory.CreateDbContextAsync();
            Assert.False(await verifyContext.DeadLetters.AnyAsync(d => d.MessageId == "old-requeued"));
            Assert.True(await verifyContext.DeadLetters.AnyAsync(d => d.MessageId == "new-requeued"));
            Assert.True(await verifyContext.DeadLetters.AnyAsync(d => d.MessageId == "still-pending"));
        }

        [Fact]
        public async Task WithNothingToDelete_ReturnsZero()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var deletedCount = await repository.DeleteRequeuedOlderThan(TimeSpan.FromDays(7));

            Assert.Equal(0, deletedCount);
        }
    }

    public class DeletePendingOlderThan
    {
        [Fact]
        public async Task DeletesOnlyPendingMessagesOlderThanAge_AndReturnsThem()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([
                new FakeDeadLetterMessage { MessageId = "old-1", EnqueuedTime = DateTimeOffset.UtcNow.AddDays(-40) },
                new FakeDeadLetterMessage { MessageId = "new-1", EnqueuedTime = DateTimeOffset.UtcNow }
            ], "orders");

            var purged = await repository.DeletePendingOlderThan(TimeSpan.FromDays(30));

            Assert.Single(purged);
            Assert.Equal("old-1", purged.Single().MessageId);
            await using var context = await factory.CreateDbContextAsync();
            Assert.False(await context.DeadLetters.AnyAsync(d => d.MessageId == "old-1"));
            Assert.True(await context.DeadLetters.AnyAsync(d => d.MessageId == "new-1"));
        }

        [Fact]
        public async Task DoesNotDeleteRequeuedMessages()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "old-requeued", EnqueuedTime = DateTimeOffset.UtcNow.AddDays(-40) }], "orders");
            await using (var context = await factory.CreateDbContextAsync())
                await context.DeadLetters
                    .Where(d => d.MessageId == "old-requeued")
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DeadLetterStatus.Requeued));

            var purged = await repository.DeletePendingOlderThan(TimeSpan.FromDays(30));

            Assert.Empty(purged);
        }

        [Fact]
        public async Task WithNothingToDelete_ReturnsEmptyCollection()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);

            var purged = await repository.DeletePendingOlderThan(TimeSpan.FromDays(30));

            Assert.Empty(purged);
        }

        [Fact]
        public async Task WithMoreCandidatesThanChunkSize_DeletesAllAcrossMultipleChunks()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var messages = Enumerable.Range(0, 501)
                .Select(i => new FakeDeadLetterMessage { MessageId = $"old-{i}", EnqueuedTime = DateTimeOffset.UtcNow.AddDays(-40) })
                .ToArray();
            await repository.SaveBatch(messages, "orders");

            var purged = await repository.DeletePendingOlderThan(TimeSpan.FromDays(30));

            Assert.Equal(501, purged.Count);
            await using var context = await factory.CreateDbContextAsync();
            Assert.Equal(0, await context.DeadLetters.CountAsync());
        }

        [Fact]
        public async Task PurgedRecordsCarryQueueNameAndMessageProperties()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new DeadLetterRepository(factory);
            var message = new FakeDeadLetterMessage
            {
                MessageId = "old-1",
                EnqueuedTime = DateTimeOffset.UtcNow.AddDays(-40),
                ApplicationProperties = new Dictionary<string, object> { ["source"] = "orders-api" }
            };
            await repository.SaveBatch([message], "orders");

            var purged = await repository.DeletePendingOlderThan(TimeSpan.FromDays(30));

            var purgedDeadLetter = purged.Single();
            Assert.Equal("orders", purgedDeadLetter.QueueName);
            Assert.Contains("orders-api", purgedDeadLetter.MessageProperties);
        }
    }
}
