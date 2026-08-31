using Flowly.DeadLetters.BackgroundServices;
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
                PendingOlderThanCountToReturn = 5
            };
            var instrumentation = new FakeDeadLetterCleanupInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions(), instrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Null(instrumentation.RequeuedPurgedCount);
            Assert.Null(instrumentation.PendingPurgedCount);
        }

        [Fact]
        public async Task WhenOnlyDeleteDeadLetteredMessagesAfterIsSet_RecordsPendingPurgedOnly()
        {
            var repository = new FakeDeadLetterRepository { PendingOlderThanCountToReturn = 3 };
            var instrumentation = new FakeDeadLetterCleanupInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions { DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30) }, instrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(3, instrumentation.PendingPurgedCount);
            Assert.Null(instrumentation.RequeuedPurgedCount);
        }

        [Fact]
        public async Task WhenOnlyDeleteRequeuedMessagesAfterIsSet_RecordsRequeuedPurgedOnly()
        {
            var repository = new FakeDeadLetterRepository { RequeuedOlderThanCountToReturn = 4 };
            var instrumentation = new FakeDeadLetterCleanupInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions { DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7) }, instrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(4, instrumentation.RequeuedPurgedCount);
            Assert.Null(instrumentation.PendingPurgedCount);
        }

        [Fact]
        public async Task WithBothOptionsSetAndNothingToDelete_DoesNotRecordAnything()
        {
            var repository = new FakeDeadLetterRepository
            {
                RequeuedOlderThanCountToReturn = 0,
                PendingOlderThanCountToReturn = 0
            };
            var instrumentation = new FakeDeadLetterCleanupInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7),
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30)
            }, instrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Null(instrumentation.RequeuedPurgedCount);
            Assert.Null(instrumentation.PendingPurgedCount);
        }

        [Fact]
        public async Task WithBothOptionsSetAndRowsDeleted_RecordsBothCounts()
        {
            var repository = new FakeDeadLetterRepository
            {
                RequeuedOlderThanCountToReturn = 2,
                PendingOlderThanCountToReturn = 6
            };
            var instrumentation = new FakeDeadLetterCleanupInstrumentation();
            var deadLetterCleanupBackgroundService = BuildService(new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7),
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30)
            }, instrumentation);

            await deadLetterCleanupBackgroundService.RunCleanup(repository, CancellationToken.None);

            Assert.Equal(2, instrumentation.RequeuedPurgedCount);
            Assert.Equal(6, instrumentation.PendingPurgedCount);
        }
    }

    private static DeadLetterCleanupBackgroundService BuildService(DeadLetterTrackingOptions opts, FakeDeadLetterCleanupInstrumentation instrumentation)
    {
        return new DeadLetterCleanupBackgroundService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(opts),
            instrumentation,
            NullLogger<DeadLetterCleanupBackgroundService>.Instance);
    }
}
