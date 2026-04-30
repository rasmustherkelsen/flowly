using Flowly.DeadLetters.BackgroundServices;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class EventSubscriptionDeadLetterIngestionSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresTopicName()
        {
            var settings = new EventSubscriptionDeadLetterIngestionSettings("orders-topic", "sub-a", "provider");

            Assert.Equal("orders-topic", settings.TopicName);
        }

        [Fact]
        public void StoresSubscriptionName()
        {
            var settings = new EventSubscriptionDeadLetterIngestionSettings("orders-topic", "sub-a", "provider");

            Assert.Equal("sub-a", settings.SubscriptionName);
        }

        [Fact]
        public void StoresProviderName()
        {
            var settings = new EventSubscriptionDeadLetterIngestionSettings("orders-topic", "sub-a", "provider");

            Assert.Equal("provider", settings.ProviderName);
        }
    }

    public class DisplayName
    {
        [Fact]
        public void CombinesTopicAndSubscription()
        {
            var settings = new EventSubscriptionDeadLetterIngestionSettings("orders-topic", "sub-a", "provider");

            Assert.Equal("orders-topic/sub-a", settings.DisplayName);
        }
    }

    public class Equality
    {
        [Fact]
        public void WithSameValues_AreEqual()
        {
            var a = new EventSubscriptionDeadLetterIngestionSettings("t", "s", "p");
            var b = new EventSubscriptionDeadLetterIngestionSettings("t", "s", "p");

            Assert.Equal(a, b);
        }

        [Fact]
        public void WithDifferentSubscription_AreNotEqual()
        {
            var a = new EventSubscriptionDeadLetterIngestionSettings("t", "s1", "p");
            var b = new EventSubscriptionDeadLetterIngestionSettings("t", "s2", "p");

            Assert.NotEqual(a, b);
        }
    }
}
