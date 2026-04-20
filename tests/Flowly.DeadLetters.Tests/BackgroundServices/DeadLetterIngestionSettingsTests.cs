using Flowly.DeadLetters.BackgroundServices;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class DeadLetterIngestionSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresQueueName()
        {
            var settings = new DeadLetterIngestionSettings("my-queue", "provider-a");

            Assert.Equal("my-queue", settings.QueueName);
        }

        [Fact]
        public void StoresProviderName()
        {
            var settings = new DeadLetterIngestionSettings("my-queue", "provider-a");

            Assert.Equal("provider-a", settings.ProviderName);
        }
    }

    public class Equality
    {
        [Fact]
        public void WithSameValues_AreEqual()
        {
            var a = new DeadLetterIngestionSettings("q", "p");
            var b = new DeadLetterIngestionSettings("q", "p");

            Assert.Equal(a, b);
        }

        [Fact]
        public void WithDifferentQueue_AreNotEqual()
        {
            var a = new DeadLetterIngestionSettings("q1", "p");
            var b = new DeadLetterIngestionSettings("q2", "p");

            Assert.NotEqual(a, b);
        }
    }
}
