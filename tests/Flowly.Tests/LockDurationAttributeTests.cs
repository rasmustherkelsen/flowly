namespace Flowly.Tests;

public class LockDurationAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void ThrowsWhenInvalidTimeSpanStringProvided()
        {
            var exception = Assert.Throws<ArgumentException>(() => new LockDurationAttribute("garbage"));

            Assert.StartsWith("Could not parse lockDuration value 'garbage' as a TimeSpan.", exception.Message);
        }
    }

    public class GetValue
    {
        [Fact]
        public void WithValidTimeSpanString_ReturnsParsedTimeSpan()
        {
            var lockDurationAttribute = new LockDurationAttribute("00:05:00");

            Assert.Equal(TimeSpan.FromMinutes(5), lockDurationAttribute.LockDuration);
        }
    }
}