using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class DefaultMessageTimeToLiveAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void StoresOriginalStringValue()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("01:00:00");

            Assert.Equal("01:00:00", defaultMessageTimeToLiveAttribute.TimeToLive);
        }
    }

    public class GetValue
    {
        [Fact]
        public void WithValidTimeSpanString_ReturnsParsedTimeSpan()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("01:30:00");

            Assert.Equal(TimeSpan.FromMinutes(90), defaultMessageTimeToLiveAttribute.GetValue());
        }

        [Fact]
        public void WithDaysFormat_ReturnsParsedTimeSpan()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("2.00:00:00");

            Assert.Equal(TimeSpan.FromDays(2), defaultMessageTimeToLiveAttribute.GetValue());
        }

        [Fact]
        public void WithInvalidTimeSpanString_ThrowsInvalidOperationException()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("not-a-timespan");

            var exception = Assert.Throws<InvalidOperationException>(() => defaultMessageTimeToLiveAttribute.GetValue());

            Assert.Contains("not-a-timespan", exception.Message);
        }
    }
}
