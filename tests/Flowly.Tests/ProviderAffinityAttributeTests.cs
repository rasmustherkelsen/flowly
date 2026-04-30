namespace Flowly.Tests;

public class ProviderAffinityAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void SetsProviderName()
        {
            var providerAffinityAttribute = new ProviderAffinityAttribute("secondary");

            Assert.Equal("secondary", providerAffinityAttribute.ProviderName);
        }

        [Fact]
        public void WithEmptyString_SetsProviderNameToEmptyString()
        {
            var providerAffinityAttribute = new ProviderAffinityAttribute(string.Empty);

            Assert.Equal(string.Empty, providerAffinityAttribute.ProviderName);
        }
    }
}