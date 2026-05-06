using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class EventTopicDescriptionTests
{
    public class Constructor
    {
        [Fact]
        public void StoresTopicName()
        {
            var eventTopicDescription = new EventTopicDescription("order-placed", null, null);

            Assert.Equal("order-placed", eventTopicDescription.TopicName);
        }

        [Fact]
        public void StoresDefaultMessageTimeToLive()
        {
            var eventTopicDescription = new EventTopicDescription("order-placed", TimeSpan.FromHours(2), null);

            Assert.Equal(TimeSpan.FromHours(2), eventTopicDescription.DefaultMessageTimeToLive);
        }

        [Fact]
        public void StoresDeadLetterOnMessageExpiration()
        {
            var eventTopicDescription = new EventTopicDescription("order-placed", null, true);

            Assert.True(eventTopicDescription.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithNullOptionalValues_PreservesNull()
        {
            var eventTopicDescription = new EventTopicDescription("order-placed", null, null);

            Assert.Null(eventTopicDescription.DefaultMessageTimeToLive);
            Assert.Null(eventTopicDescription.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithDeadLetterOnMessageExpirationFalse_StoresFalse()
        {
            var eventTopicDescription = new EventTopicDescription("order-placed", null, false);

            Assert.False(eventTopicDescription.DeadLetterOnMessageExpiration);
        }
    }
}
