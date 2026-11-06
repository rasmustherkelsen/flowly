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
}
