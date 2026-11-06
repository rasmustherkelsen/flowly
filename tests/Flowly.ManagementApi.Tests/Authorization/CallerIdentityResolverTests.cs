using System.Security.Claims;
using Flowly.ManagementApi.Authorization;

namespace Flowly.ManagementApi.Tests.Authorization;

public class CallerIdentityResolverTests
{
    public class Resolve
    {
        [Fact]
        public void ForOAuth2WithAName_ReturnsTheName()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.Name, "alice"), new Claim("sub", "subject-1"), new Claim("azp", "client-1"));

            Assert.Equal("alice", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForOAuth2WithoutANameButASubject_ReturnsTheSubject()
        {
            var user = AuthenticatedUser(new Claim("sub", "subject-1"), new Claim("azp", "client-1"));

            Assert.Equal("subject-1", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForOAuth2WithAMappedNameIdentifier_ReturnsTheNameIdentifier()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.NameIdentifier, "subject-1"), new Claim("azp", "client-1"));

            Assert.Equal("subject-1", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForOAuth2WithOnlyAnAuthorizedParty_ReturnsTheAuthorizedParty()
        {
            var user = AuthenticatedUser(new Claim("azp", "client-1"));

            Assert.Equal("client-1", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForOAuth2WithBlankClaims_SkipsThemInOrder()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.Name, " "), new Claim("sub", ""), new Claim("azp", "client-1"));

            Assert.Equal("client-1", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForOAuth2WithoutAnyIdentityClaim_ReturnsNull()
        {
            var user = AuthenticatedUser(new Claim("roles", "Flowly.Management.Write"));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForExistingPoliciesWithAName_ReturnsTheName()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.Name, "bob"));

            Assert.Equal("bob", CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ExistingPolicies));
        }

        [Fact]
        public void ForExistingPoliciesWithoutAName_IgnoresTheSubjectAndAuthorizedParty()
        {
            var user = AuthenticatedUser(new Claim("sub", "subject-1"), new Claim(ClaimTypes.NameIdentifier, "subject-1"), new Claim("azp", "client-1"));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ExistingPolicies));
        }

        [Fact]
        public void ForApiKeyEvenWithANameClaim_ReturnsNull()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.Name, "alice"));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ApiKey));
        }

        [Fact]
        public void ForAnUnauthenticatedPrincipalWithOAuth2_ReturnsNull()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")]));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForAnUnauthenticatedPrincipalWithExistingPolicies_ReturnsNull()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")]));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ExistingPolicies));
        }

        [Fact]
        public void ForAnUnauthenticatedPrincipalWithApiKey_ReturnsNull()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")]));

            Assert.Null(CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ApiKey));
        }

        [Fact]
        public void ForANullPrincipal_ReturnsNull()
        {
            Assert.Null(CallerIdentityResolver.Resolve(null, ManagementApiAuthMode.OAuth2));
        }

        [Fact]
        public void ForANameLongerThanTheAuditField_TruncatesItToTheMaximumLength()
        {
            var user = AuthenticatedUser(new Claim(ClaimTypes.Name, new string('a', CallerIdentityResolver.MaxLength + 50)));

            Assert.Equal(new string('a', CallerIdentityResolver.MaxLength), CallerIdentityResolver.Resolve(user, ManagementApiAuthMode.ExistingPolicies));
        }
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
}
