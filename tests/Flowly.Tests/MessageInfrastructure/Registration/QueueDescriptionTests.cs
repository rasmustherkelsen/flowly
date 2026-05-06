using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class QueueDescriptionTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var queueDescription = new QueueDescription(
                name: "orders",
                defaultMessageTimeToLive: TimeSpan.FromHours(2),
                deadLetterOnMessageExpiration: true,
                lockDuration: TimeSpan.FromMinutes(5),
                requiresSession: true);

            Assert.Equal("orders", queueDescription.Name);
            Assert.Equal(TimeSpan.FromHours(2), queueDescription.DefaultMessageTimeToLive);
            Assert.True(queueDescription.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), queueDescription.LockDuration);
            Assert.True(queueDescription.RequiresSession);
        }

        [Fact]
        public void WithRequiresSessionFalse_StoresFalse()
        {
            var queueDescription = new QueueDescription(
                name: "orders",
                defaultMessageTimeToLive: TimeSpan.FromHours(2),
                deadLetterOnMessageExpiration: false,
                lockDuration: TimeSpan.FromMinutes(5),
                requiresSession: false);

            Assert.False(queueDescription.RequiresSession);
            Assert.False(queueDescription.DeadLetterOnMessageExpiration);
        }
    }
}
