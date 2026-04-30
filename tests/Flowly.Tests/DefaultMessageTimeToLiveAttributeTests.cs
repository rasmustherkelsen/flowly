namespace Flowly.Tests;

public class DefaultMessageTimeToLiveAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void ThrowsIfTimeSpanStringIsInvalid()
        {
            var exception = Assert.Throws<ArgumentException>(() => new DefaultMessageTimeToLiveAttribute("not-a-timespan"));

            Assert.Equal("Could not parse timeToLive value 'not-a-timespan' as a TimeSpan.", exception.Message);
        }
    }

    public class TimeToLive
    {
        [Fact]
        public void WithValidTimeSpanString_ReturnsParsedTimeSpan()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("01:30:00");

            Assert.Equal(TimeSpan.FromMinutes(90), defaultMessageTimeToLiveAttribute.TimeToLive);
        }

        [Fact]
        public void WithDaysFormat_ReturnsParsedTimeSpan()
        {
            var defaultMessageTimeToLiveAttribute = new DefaultMessageTimeToLiveAttribute("2.00:00:00");

            Assert.Equal(TimeSpan.FromDays(2), defaultMessageTimeToLiveAttribute.TimeToLive);
        }
    }
}