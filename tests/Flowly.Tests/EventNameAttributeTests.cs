namespace Flowly.Tests;

public class EventNameAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsName()
        {
            var eventNameAttribute = new EventNameAttribute("order-placed");

            Assert.Equal("order-placed", eventNameAttribute.Name);
        }

        [Fact]
        public void WithEmptyString_StoresEmptyString()
        {
            var eventNameAttribute = new EventNameAttribute(string.Empty);

            Assert.Equal(string.Empty, eventNameAttribute.Name);
        }
    }

    public class Usage
    {
        [Fact]
        public void IsApplicableToClassesOnly()
        {
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(typeof(EventNameAttribute), typeof(AttributeUsageAttribute));

            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        }

        [Fact]
        public void IsSealed()
        {
            Assert.True(typeof(EventNameAttribute).IsSealed);
        }
    }
}
