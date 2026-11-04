namespace Flowly.DeadLetters.Tests;

public class DeadLetterQueryTests
{
    public class Defaults
    {
        [Fact]
        public void DoNotFilterOrPageAndSkipTheTotalCount()
        {
            var deadLetterQuery = new DeadLetterQuery();

            Assert.Null(deadLetterQuery.QueueName);
            Assert.Null(deadLetterQuery.SubscriptionName);
            Assert.Null(deadLetterQuery.Status);
            Assert.Null(deadLetterQuery.MessageId);
            Assert.Equal(0, deadLetterQuery.Skip);
            Assert.Null(deadLetterQuery.Take);
            Assert.False(deadLetterQuery.IncludeTotalCount);
        }
    }

    public class Skip
    {
        [Fact]
        public void WithNegativeValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeadLetterQuery { Skip = -1 });
        }

        [Fact]
        public void WithZero_IsAccepted()
        {
            var deadLetterQuery = new DeadLetterQuery { Skip = 0 };

            Assert.Equal(0, deadLetterQuery.Skip);
        }
    }

    public class Take
    {
        [Fact]
        public void WithNegativeValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeadLetterQuery { Take = -1 });
        }

        [Fact]
        public void WithZero_IsAccepted()
        {
            var deadLetterQuery = new DeadLetterQuery { Take = 0 };

            Assert.Equal(0, deadLetterQuery.Take);
        }

        [Fact]
        public void WithNull_IsAccepted()
        {
            var deadLetterQuery = new DeadLetterQuery { Take = null };

            Assert.Null(deadLetterQuery.Take);
        }
    }
}
