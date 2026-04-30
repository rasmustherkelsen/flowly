namespace Flowly.AzureServiceBus.Tests;

public class AzureServiceBusMaxMessageSizeTests
{
    public class StandardConstant
    {
        [Fact]
        public void Equals256Kilobytes() => Assert.Equal(256L * 1024, AzureServiceBusMaxMessageSize.Standard);
    }

    public class PremiumConstant
    {
        [Fact]
        public void Equals1Megabyte() => Assert.Equal(1L * 1024 * 1024, AzureServiceBusMaxMessageSize.Premium);
    }

    public class PremiumLargeMessagesConstant
    {
        [Fact]
        public void Equals100Megabytes() => Assert.Equal(100L * 1024 * 1024, AzureServiceBusMaxMessageSize.PremiumLargeMessages);
    }
}
