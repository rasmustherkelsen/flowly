using Flowly.MessageInfrastructure.Events.Registration;

namespace Flowly.Tests.MessageInfrastructure.Events.Registration;

public class DeferredEventRegistrationTests
{
    public class Constructor
    {
        [Fact]
        public void WithRequiredArguments_OptionalValuesAreNull()
        {
            var deferredEventRegistration = new DeferredEventRegistration("order-placed", "audit");

            Assert.Equal("order-placed", deferredEventRegistration.TopicName);
            Assert.Equal("audit", deferredEventRegistration.SubscriptionName);
            Assert.Null(deferredEventRegistration.DefaultMessageTimeToLive);
            Assert.Null(deferredEventRegistration.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithAllArguments_StoresEachValue()
        {
            var deferredEventRegistration = new DeferredEventRegistration(
                TopicName: "order-placed",
                SubscriptionName: "audit",
                DefaultMessageTimeToLive: TimeSpan.FromDays(1),
                DeadLetterOnMessageExpiration: true);

            Assert.Equal(TimeSpan.FromDays(1), deferredEventRegistration.DefaultMessageTimeToLive);
            Assert.True(deferredEventRegistration.DeadLetterOnMessageExpiration);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new DeferredEventRegistration("t", "s");
            var second = new DeferredEventRegistration("t", "s");

            Assert.Equal(first, second);
        }
    }
}
