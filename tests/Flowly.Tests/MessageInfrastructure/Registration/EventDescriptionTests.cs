using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class EventDescriptionTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var eventDescription = new EventDescription(
                "order-placed",
                "audit-log",
                TimeSpan.FromHours(4),
                true);

            Assert.Equal("order-placed", eventDescription.TopicName);
            Assert.Equal("audit-log", eventDescription.SubscriptionName);
            Assert.Equal(TimeSpan.FromHours(4), eventDescription.DefaultMessageTimeToLive);
            Assert.True(eventDescription.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithOptionalValuesNull_LeavesThemNull()
        {
            var eventDescription = new EventDescription(
                "order-placed",
                "audit-log",
                null,
                null);

            Assert.Null(eventDescription.DefaultMessageTimeToLive);
            Assert.Null(eventDescription.DeadLetterOnMessageExpiration);
        }
    }
}