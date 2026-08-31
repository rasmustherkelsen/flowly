using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Telemetry;
using Flowly.DeadLetters.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class DeadLetterCleanupBackgroundServiceTests
{
    public class RunCleanup
    {
        [Fact]
        public async Task WhenNeitherRetentionOptionIsSet_DoesNotDeleteOrRecordAnything()
        {
            var repository = new FakeDeadLetterRepository
            {
                RequeuedOlderThanCountToReturn = 5,
                PendingOlderThanToReturn = [BuildDeadLetter("msg-1")]
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions(), cleanupInstrumentation, operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Null(cleanupInstrumentation.RequeuedPurgedCount);
            Assert.Null(cleanupInstrumentation.PendingPurgedCount);
            Assert.Empty(operationInstrumentation.Discarded);
        }

        [Fact]
        public async Task WhenOnlyDeleteDeadLetteredMessagesAfterIsSet_RecordsPendingPurgedOnly()
        {
            var repository = new FakeDeadLetterRepository
            {
                PendingOlderThanToReturn = [BuildDeadLetter("msg-1"), BuildDeadLetter("msg-2"), BuildDeadLetter("msg-3")]
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(
                new DeadLetterTrackingOptions { DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30) },
                cleanupInstrumentation,
                operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(3, cleanupInstrumentation.PendingPurgedCount);
            Assert.Null(cleanupInstrumentation.RequeuedPurgedCount);
        }

        [Fact]
        public async Task WhenOnlyDeleteRequeuedMessagesAfterIsSet_RecordsRequeuedPurgedOnly()
        {
            var repository = new FakeDeadLetterRepository { RequeuedOlderThanCountToReturn = 4 };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(
                new DeadLetterTrackingOptions { DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7) },
                cleanupInstrumentation,
                operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(4, cleanupInstrumentation.RequeuedPurgedCount);
            Assert.Null(cleanupInstrumentation.PendingPurgedCount);
            Assert.Empty(operationInstrumentation.Discarded);
        }

        [Fact]
        public async Task WithBothOptionsSetAndNothingToDelete_DoesNotRecordAnything()
        {
            var repository = new FakeDeadLetterRepository
            {
                RequeuedOlderThanCountToReturn = 0,
                PendingOlderThanToReturn = []
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7),
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30)
            }, cleanupInstrumentation, operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Null(cleanupInstrumentation.RequeuedPurgedCount);
            Assert.Null(cleanupInstrumentation.PendingPurgedCount);
            Assert.Empty(operationInstrumentation.Discarded);
        }

        [Fact]
        public async Task WithBothOptionsSetAndRowsDeleted_RecordsBothCounts()
        {
            var repository = new FakeDeadLetterRepository
            {
                RequeuedOlderThanCountToReturn = 2,
                PendingOlderThanToReturn = [BuildDeadLetter("msg-1"), BuildDeadLetter("msg-2"), BuildDeadLetter("msg-3"),
                    BuildDeadLetter("msg-4"), BuildDeadLetter("msg-5"), BuildDeadLetter("msg-6")]
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7),
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30)
            }, cleanupInstrumentation, operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(2, cleanupInstrumentation.RequeuedPurgedCount);
            Assert.Equal(6, cleanupInstrumentation.PendingPurgedCount);
        }

        [Fact]
        public async Task WithExpiredPendingDeadLetters_RecordsDiscardPerMessageWithExpiredReason()
        {
            var repository = new FakeDeadLetterRepository
            {
                PendingOlderThanToReturn = [BuildDeadLetter("msg-1", "orders"), BuildDeadLetter("msg-2", "orders")]
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(
                new DeadLetterTrackingOptions { DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30) },
                cleanupInstrumentation,
                operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(2, operationInstrumentation.Discarded.Count);
            Assert.All(operationInstrumentation.Discarded, discard => Assert.Equal(DeadLetterDiscardReason.Expired, discard.Reason));
            Assert.Contains(operationInstrumentation.DiscardStarted, started => started is ("orders", "msg-1"));
            Assert.Contains(operationInstrumentation.DiscardStarted, started => started is ("orders", "msg-2"));
        }

        [Fact]
        public async Task WithExpiredPendingDeadLetters_WhenRecordingThrowsForOneMessage_StillRecordsRemainingDiscards()
        {
            var repository = new FakeDeadLetterRepository
            {
                PendingOlderThanToReturn = [BuildDeadLetter("msg-1", "orders"), BuildDeadLetter("msg-2", "orders"), BuildDeadLetter("msg-3", "orders")]
            };
            var cleanupInstrumentation = new FakeDeadLetterCleanupInstrumentation();
            var operationInstrumentation = new FakeDeadLetterOperationInstrumentation();
            operationInstrumentation.MessageIdsToThrowFor.Add("msg-2");
            var deadLetterCleanupBackgroundService = BuildService(
                new DeadLetterTrackingOptions { DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30) },
                cleanupInstrumentation,
                operationInstrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(2, operationInstrumentation.Discarded.Count);
            Assert.Contains(operationInstrumentation.DiscardStarted, started => started is ("orders", "msg-1"));
            Assert.Contains(operationInstrumentation.DiscardStarted, started => started is ("orders", "msg-3"));
            Assert.DoesNotContain(operationInstrumentation.DiscardStarted, started => started is ("orders", "msg-2"));
        }
    }

    private static DeadLetterCleanupBackgroundService BuildService(
        DeadLetterTrackingOptions opts,
        FakeDeadLetterCleanupInstrumentation cleanupInstrumentation,
        FakeDeadLetterOperationInstrumentation operationInstrumentation)
    {
        return new DeadLetterCleanupBackgroundService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(opts),
            cleanupInstrumentation,
            operationInstrumentation,
            NullLogger<DeadLetterCleanupBackgroundService>.Instance);
    }

    private static PurgedDeadLetter BuildDeadLetter(string messageId, string queueName = "test-queue")
    {
        return new PurgedDeadLetter(messageId, queueName, "{}");
    }
}
