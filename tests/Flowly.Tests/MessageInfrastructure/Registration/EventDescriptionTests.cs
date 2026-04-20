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
                topicOrExchangeName: "order-placed",
                subscriptionName: "audit-log",
                defaultMessageTimeToLive: TimeSpan.FromHours(4),
                deadLetterOnMessageExpiration: true);

            Assert.Equal("order-placed", eventDescription.TopicOrExchangeName);
            Assert.Equal("audit-log", eventDescription.SubscriptionName);
            Assert.Equal(TimeSpan.FromHours(4), eventDescription.DefaultMessageTimeToLive);
            Assert.True(eventDescription.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithOptionalValuesNull_LeavesThemNull()
        {
            var eventDescription = new EventDescription(
                topicOrExchangeName: "order-placed",
                subscriptionName: "audit-log",
                defaultMessageTimeToLive: null,
                deadLetterOnMessageExpiration: null);

            Assert.Null(eventDescription.DefaultMessageTimeToLive);
            Assert.Null(eventDescription.DeadLetterOnMessageExpiration);
        }
    }
}
