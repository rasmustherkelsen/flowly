using Flowly.Dashboard.Auth;

namespace Flowly.Dashboard.Tests.Auth;

public class OAuthAuthenticationOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void WithNullClientId_ThrowsArgumentNullException()
            => Assert.Throws<ArgumentNullException>(() => new OAuthAuthenticationOptions(clientId: null!, authority: "https://accounts.google.com"));

        [Fact]
        public void WithWhitespaceClientId_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() => new OAuthAuthenticationOptions(clientId: "   ", authority: "https://accounts.google.com"));

        [Fact]
        public void WithEmptyClientId_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() => new OAuthAuthenticationOptions(clientId: "", authority: "https://accounts.google.com"));

        [Fact]
        public void WithNullAuthority_ThrowsArgumentNullException()
            => Assert.Throws<ArgumentNullException>(() => new OAuthAuthenticationOptions(clientId: "client", authority: null!));

        [Fact]
        public void WithWhitespaceAuthority_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() => new OAuthAuthenticationOptions(clientId: "client", authority: "   "));

        [Fact]
        public void WithEmptyAuthority_ThrowsArgumentException()
            => Assert.Throws<ArgumentException>(() => new OAuthAuthenticationOptions(clientId: "client", authority: ""));

        [Fact]
        public void WithClientIdAndAuthority_SetsClientId()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(clientId: "my-client", authority: "https://accounts.google.com");

            Assert.Equal("my-client", oAuthAuthenticationOptions.ClientId);
        }

        [Fact]
        public void WithClientIdAndAuthority_SetsAuthority()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(clientId: "my-client", authority: "https://accounts.google.com");

            Assert.Equal("https://accounts.google.com", oAuthAuthenticationOptions.Authority);
        }

        [Fact]
        public void WithoutClientSecret_DefaultsClientSecretToNull()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(clientId: "my-client", authority: "https://accounts.google.com");

            Assert.Null(oAuthAuthenticationOptions.ClientSecret);
        }

        [Fact]
        public void WithClientSecret_SetsClientSecret()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(
                clientId: "my-client",
                authority: "https://accounts.google.com",
                clientSecret: "top-secret");

            Assert.Equal("top-secret", oAuthAuthenticationOptions.ClientSecret);
        }

        [Fact]
        public void WithAllRolesAndPolicies_SetsEveryProperty()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(
                clientId: "my-client",
                authority: "https://accounts.google.com",
                clientSecret: "top-secret",
                viewerRoles: ["viewer-role"],
                viewerPolicies: ["viewer-policy"],
                submitterRoles: ["submitter-role"],
                submitterPolicies: ["submitter-policy"]);

            Assert.Equal(["viewer-role"], oAuthAuthenticationOptions.ViewerRoles);
            Assert.Equal(["viewer-policy"], oAuthAuthenticationOptions.ViewerPolicies);
            Assert.Equal(["submitter-role"], oAuthAuthenticationOptions.SubmitterRoles);
            Assert.Equal(["submitter-policy"], oAuthAuthenticationOptions.SubmitterPolicies);
        }

        [Fact]
        public void WithoutRolesOrPolicies_DefaultsAllToNull()
        {
            var oAuthAuthenticationOptions = new OAuthAuthenticationOptions(clientId: "my-client", authority: "https://accounts.google.com");

            Assert.Null(oAuthAuthenticationOptions.ViewerRoles);
            Assert.Null(oAuthAuthenticationOptions.ViewerPolicies);
            Assert.Null(oAuthAuthenticationOptions.SubmitterRoles);
            Assert.Null(oAuthAuthenticationOptions.SubmitterPolicies);
        }
    }
}
