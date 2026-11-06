using Flowly.DeadLetters.DatabaseModel;
using Flowly.DeadLetters.Services;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterQueryEvaluatorTests
{
    private static readonly DateTimeOffset Baseline = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static DeadLetter Build(
        string messageId,
        int minutes = 0,
        string queueName = "orders",
        string? subscriptionName = null,
        DeadLetterStatus status = DeadLetterStatus.Pending)
        => new()
        {
            MessageId = messageId,
            QueueName = queueName,
            SubscriptionName = subscriptionName,
            MessageBody = "{}",
            MessageProperties = "{}",
            DeadLetteredAt = Baseline.AddMinutes(minutes),
            Status = status
        };

    public class Evaluate
    {
        [Fact]
        public void WithNoDeadLetters_ReturnsEmptyPage()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([], new DeadLetterQuery { IncludeTotalCount = true });

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public void ReturnsNewestFirstWithOrdinalMessageIdAsTieBreaker()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([Build("b"), Build("c", 5), Build("a"), Build("B")], new DeadLetterQuery());

            Assert.Equal(["c", "B", "a", "b"], result.Items.Select(d => d.MessageId));
        }

        [Fact]
        public void FiltersByEveryField()
        {
            var deadLetters = new[]
            {
                Build("a", queueName: "orders"),
                Build("b", queueName: "orders", status: DeadLetterStatus.Requeued),
                Build("c", queueName: "topic", subscriptionName: "billing"),
                Build("d", queueName: "topic", subscriptionName: "shipping")
            };

            Assert.Equal(["a", "b"], DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { QueueName = "orders" }).Items.Select(d => d.MessageId));
            Assert.Equal(["d"], DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { SubscriptionName = "shipping" }).Items.Select(d => d.MessageId));
            Assert.Equal(["b"], DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { Status = DeadLetterStatus.Requeued }).Items.Select(d => d.MessageId));
            Assert.Equal(["c"], DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { MessageId = "c" }).Items.Select(d => d.MessageId));
        }

        [Fact]
        public void PagesWithSkipAndTake()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([Build("a", 4), Build("b", 3), Build("c", 2), Build("d", 1)], new DeadLetterQuery { Skip = 1, Take = 2 });

            Assert.Equal(["b", "c"], result.Items.Select(d => d.MessageId));
        }

        [Fact]
        public void WithoutTake_ReturnsOnlyTheDefaultPageSize()
        {
            var deadLetters = Enumerable.Range(0, DeadLetterQuery.DefaultTake + 1).Select(i => Build($"m{i:D3}", i)).ToArray();

            var result = DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { IncludeTotalCount = true });

            Assert.Equal(DeadLetterQuery.DefaultTake, result.Items.Count);
            Assert.Equal(DeadLetterQuery.DefaultTake + 1, result.TotalCount);
        }

        [Fact]
        public void WithTakeAboveTheDefault_HonorsTheExplicitTake()
        {
            var deadLetters = Enumerable.Range(0, DeadLetterQuery.DefaultTake + 5).Select(i => Build($"m{i:D3}", i)).ToArray();

            var result = DeadLetterQueryEvaluator.Evaluate(deadLetters, new DeadLetterQuery { Take = DeadLetterQuery.DefaultTake + 5 });

            Assert.Equal(DeadLetterQuery.DefaultTake + 5, result.Items.Count);
        }

        [Fact]
        public void WithSkipBeyondTheEnd_ReturnsEmptyPageButFullTotal()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([Build("a"), Build("b")], new DeadLetterQuery { Skip = 5, Take = 2, IncludeTotalCount = true });

            Assert.Empty(result.Items);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public void TotalCountCountsTheFilteredSetIgnoringPaging()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([Build("a"), Build("b"), Build("c", queueName: "invoices")], new DeadLetterQuery { QueueName = "orders", Take = 1, IncludeTotalCount = true });

            Assert.Single(result.Items);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public void WithoutIncludeTotalCount_LeavesTotalCountNull()
        {
            var result = DeadLetterQueryEvaluator.Evaluate([Build("a")], new DeadLetterQuery());

            Assert.Null(result.TotalCount);
        }
    }
}
