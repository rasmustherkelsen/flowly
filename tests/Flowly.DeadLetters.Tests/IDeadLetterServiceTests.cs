using Flowly.DeadLetters.DatabaseModel;

namespace Flowly.DeadLetters.Tests;

public class IDeadLetterServiceTests
{
    public class QueryDeadLetters
    {
        [Fact]
        public async Task OnAnImplementationWithoutItsOwn_FallsBackToFilteringGetDeadLettersInMemory()
        {
            IDeadLetterService legacyDeadLetterService = new LegacyDeadLetterService(
                Build("a", 1, "orders"),
                Build("b", 3, "orders"),
                Build("c", 2, "invoices"),
                Build("d", 4, "orders"));

            var result = await legacyDeadLetterService.QueryDeadLetters(new DeadLetterQuery { QueueName = "orders", Skip = 1, Take = 1, IncludeTotalCount = true });

            Assert.Equal("b", Assert.Single(result.Items).MessageId);
            Assert.Equal(3, result.TotalCount);
        }

        [Fact]
        public async Task OnAnImplementationWithoutItsOwn_ForwardsTheCancellationTokenToGetDeadLetters()
        {
            var legacyDeadLetterService = new LegacyDeadLetterService();
            using var cancellationTokenSource = new CancellationTokenSource();

            await ((IDeadLetterService)legacyDeadLetterService).QueryDeadLetters(new DeadLetterQuery(), cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, legacyDeadLetterService.ReceivedToken);
        }
    }

    private static DeadLetter Build(string messageId, int minutes, string queueName)
        => new()
        {
            MessageId = messageId,
            QueueName = queueName,
            MessageBody = "{}",
            MessageProperties = "{}",
            DeadLetteredAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero).AddMinutes(minutes)
        };

    private sealed class LegacyDeadLetterService(params DeadLetter[] deadLetters) : IDeadLetterService
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<IReadOnlyCollection<IDeadLetter>> GetDeadLetters(CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;

            return Task.FromResult<IReadOnlyCollection<IDeadLetter>>(deadLetters);
        }

        public Task Requeue(string messageId, string? requeuedBy = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Discard(string messageId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
