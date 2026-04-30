using Flowly.DeadLetters.DatabaseModel;

namespace Flowly.DeadLetters.Tests.DatabaseModel;

public class DeadLetterTests
{
    public class Initialization
    {
        [Fact]
        public void DefaultStatus_IsPending()
        {
            var deadLetter = BuildDeadLetter();

            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
        }

        [Fact]
        public void StoresRequiredProperties()
        {
            var enqueuedAt = DateTimeOffset.UtcNow;
            var deadLetter = new DeadLetter
            {
                MessageId = "msg-1",
                QueueName = "my-queue",
                MessageBody = "{\"key\":\"val\"}",
                MessageProperties = "{}",
                DeadLetteredAt = enqueuedAt
            };

            Assert.Equal("msg-1", deadLetter.MessageId);
            Assert.Equal("my-queue", deadLetter.QueueName);
            Assert.Equal("{\"key\":\"val\"}", deadLetter.MessageBody);
            Assert.Equal(enqueuedAt, deadLetter.DeadLetteredAt);
        }

        [Fact]
        public void OptionalProperties_DefaultToNull()
        {
            var deadLetter = BuildDeadLetter();

            Assert.Null(deadLetter.SubscriptionName);
            Assert.Null(deadLetter.DeadLetterReason);
            Assert.Null(deadLetter.DeadLetterErrorDescription);
            Assert.Null(deadLetter.RequeuedAt);
            Assert.Null(deadLetter.RequeuedBy);
        }
    }

    public class StatusTransition
    {
        [Fact]
        public void CanSetStatusToRequeued()
        {
            var deadLetter = BuildDeadLetter();

            deadLetter.Status = DeadLetterStatus.Requeued;

            Assert.Equal(DeadLetterStatus.Requeued, deadLetter.Status);
        }

        [Fact]
        public void CanSetRequeuedAt()
        {
            var deadLetter = BuildDeadLetter();
            var now = DateTimeOffset.UtcNow;

            deadLetter.RequeuedAt = now;

            Assert.Equal(now, deadLetter.RequeuedAt);
        }

        [Fact]
        public void CanSetRequeuedBy()
        {
            var deadLetter = BuildDeadLetter();

            deadLetter.RequeuedBy = "operator@example.com";

            Assert.Equal("operator@example.com", deadLetter.RequeuedBy);
        }
    }

    private static DeadLetter BuildDeadLetter() => new()
    {
        MessageId = "msg-1",
        QueueName = "queue",
        MessageBody = "{}",
        MessageProperties = "{}",
        DeadLetteredAt = DateTimeOffset.UtcNow
    };
}
