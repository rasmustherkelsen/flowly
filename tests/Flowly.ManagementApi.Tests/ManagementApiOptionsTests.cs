namespace Flowly.ManagementApi.Tests;

public class ManagementApiOptionsTests
{
    public class RequireApiKeyWithSingleKey
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BlankKey_Throws(string? apiKey)
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.ThrowsAny<ArgumentException>(() => managementApiOptions.RequireApiKey(apiKey!));
        }

        [Fact]
        public void ValidKey_SetsBothKeysAndApiKeyMode()
        {
            var managementApiOptions = new ManagementApiOptions();

            managementApiOptions.RequireApiKey("secret");

            Assert.Equal("secret", managementApiOptions.ReadApiKey);
            Assert.Equal("secret", managementApiOptions.WriteApiKey);
            Assert.Equal(ManagementApiAuthMode.ApiKey, managementApiOptions.AuthMode);
        }
    }

    public class RequireApiKeyWithReadAndWriteKeys
    {
        [Theory]
        [InlineData(null, "write")]
        [InlineData("", "write")]
        [InlineData("   ", "write")]
        [InlineData("read", null)]
        [InlineData("read", "")]
        [InlineData("read", "   ")]
        public void BlankKey_Throws(string? readApiKey, string? writeApiKey)
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.ThrowsAny<ArgumentException>(() => managementApiOptions.RequireApiKey(readApiKey!, writeApiKey!));
        }

        [Fact]
        public void EqualKeys_Throws()
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.Throws<ArgumentException>(() => managementApiOptions.RequireApiKey("same-key", "same-key"));
        }

        [Fact]
        public void EqualKeys_DoesNotSelectApiKeyMode()
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.ThrowsAny<ArgumentException>(() => managementApiOptions.RequireApiKey("same-key", "same-key"));

            Assert.Equal(ManagementApiAuthMode.None, managementApiOptions.AuthMode);
        }

        [Theory]
        [InlineData("key", "KEY")]
        [InlineData("key", "key ")]
        [InlineData("read-key", "write-key")]
        public void NearEqualKeys_AreAccepted(string readApiKey, string writeApiKey)
        {
            var managementApiOptions = new ManagementApiOptions();

            managementApiOptions.RequireApiKey(readApiKey, writeApiKey);

            Assert.Equal(readApiKey, managementApiOptions.ReadApiKey);
            Assert.Equal(writeApiKey, managementApiOptions.WriteApiKey);
        }

        [Fact]
        public void ValidKeys_SetsEachKeyAndApiKeyMode()
        {
            var managementApiOptions = new ManagementApiOptions();

            managementApiOptions.RequireApiKey("read", "write");

            Assert.Equal("read", managementApiOptions.ReadApiKey);
            Assert.Equal("write", managementApiOptions.WriteApiKey);
            Assert.Equal(ManagementApiAuthMode.ApiKey, managementApiOptions.AuthMode);
        }
    }
}
