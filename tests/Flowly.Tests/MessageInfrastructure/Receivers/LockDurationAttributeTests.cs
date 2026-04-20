using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class LockDurationAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void StoresOriginalStringValue()
        {
            var lockDurationAttribute = new LockDurationAttribute("00:05:00");

            Assert.Equal("00:05:00", lockDurationAttribute.LockDuration);
        }
    }

    public class GetValue
    {
        [Fact]
        public void WithValidTimeSpanString_ReturnsParsedTimeSpan()
        {
            var lockDurationAttribute = new LockDurationAttribute("00:05:00");

            Assert.Equal(TimeSpan.FromMinutes(5), lockDurationAttribute.GetValue());
        }

        [Fact]
        public void WithInvalidTimeSpanString_ThrowsInvalidOperationException()
        {
            var lockDurationAttribute = new LockDurationAttribute("garbage");

            var exception = Assert.Throws<InvalidOperationException>(() => lockDurationAttribute.GetValue());

            Assert.Contains("garbage", exception.Message);
        }
    }
}
