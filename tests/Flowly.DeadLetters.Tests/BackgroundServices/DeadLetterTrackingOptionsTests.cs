using Flowly.DeadLetters.BackgroundServices;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class DeadLetterTrackingOptionsTests
{
    public class Defaults
    {
        [Fact]
        public void DeleteRequeuedMessagesAfter_IsNull()
        {
            var deadLetterTrackingOptions = new DeadLetterTrackingOptions();

            Assert.Null(deadLetterTrackingOptions.DeleteRequeuedMessagesAfter);
        }

        [Fact]
        public void DeleteDeadLetteredMessagesAfter_IsNull()
        {
            var deadLetterTrackingOptions = new DeadLetterTrackingOptions();

            Assert.Null(deadLetterTrackingOptions.DeleteDeadLetteredMessagesAfter);
        }
    }

    public class AssigningProperties
    {
        [Fact]
        public void DeleteRequeuedMessagesAfter_CanBeSet()
        {
            var deadLetterTrackingOptions = new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7)
            };

            Assert.Equal(TimeSpan.FromDays(7), deadLetterTrackingOptions.DeleteRequeuedMessagesAfter);
        }

        [Fact]
        public void DeleteDeadLetteredMessagesAfter_CanBeSet()
        {
            var deadLetterTrackingOptions = new DeadLetterTrackingOptions
            {
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30)
            };

            Assert.Equal(TimeSpan.FromDays(30), deadLetterTrackingOptions.DeleteDeadLetteredMessagesAfter);
        }

        [Fact]
        public void BothProperties_CanBeSetIndependently()
        {
            var deadLetterTrackingOptions = new DeadLetterTrackingOptions
            {
                DeleteRequeuedMessagesAfter = TimeSpan.FromDays(1),
                DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(2)
            };

            Assert.Equal(TimeSpan.FromDays(1), deadLetterTrackingOptions.DeleteRequeuedMessagesAfter);
            Assert.Equal(TimeSpan.FromDays(2), deadLetterTrackingOptions.DeleteDeadLetteredMessagesAfter);
        }
    }
}
