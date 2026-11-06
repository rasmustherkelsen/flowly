namespace Flowly.ManagementApi.Tests;

public class ManagementApiOptionsTests
{
    public class UseOAuth2
    {
        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void WithBlankAuthority_Throws(string authority)
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.Throws<InvalidOperationException>(() => managementApiOptions.UseOAuth2(oauth =>
            {
                oauth.Authority = authority;
                oauth.Audience = "api://flowly";
            }));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void WithBlankAudience_Throws(string audience)
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.Throws<InvalidOperationException>(() => managementApiOptions.UseOAuth2(oauth =>
            {
                oauth.Authority = "https://issuer.test";
                oauth.Audience = audience;
            }));
        }

        [Fact]
        public void WithoutConfiguringAuthorityAndAudience_Throws()
        {
            var managementApiOptions = new ManagementApiOptions();

            Assert.Throws<InvalidOperationException>(() => managementApiOptions.UseOAuth2(_ => { }));
        }

        [Fact]
        public void WithAuthorityAndAudience_SelectsOAuth2Mode()
        {
            var managementApiOptions = new ManagementApiOptions();

            managementApiOptions.UseOAuth2(oauth =>
            {
                oauth.Authority = "https://issuer.test";
                oauth.Audience = "api://flowly";
            });

            Assert.Equal(ManagementApiAuthMode.OAuth2, managementApiOptions.AuthMode);
        }
    }

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
