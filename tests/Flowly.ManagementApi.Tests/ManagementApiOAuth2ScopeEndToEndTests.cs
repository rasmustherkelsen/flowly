using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Flowly.ManagementApi.Tests.Fakes;

namespace Flowly.ManagementApi.Tests;

public class ManagementApiOAuth2ScopeEndToEndTests
{
    private const string ListPath = "/flowly/management/dead-letters";
    private const string RequeuePath = "/flowly/management/dead-letters/requeue";
    private const string ReadValue = "Flowly.Management.Read";
    private const string WriteValue = "Flowly.Management.Write";

    public class WithoutAToken
    {
        [Fact]
        public async Task Returns401()
        {
            using var oAuth2TestHost = new OAuth2TestHost();

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class ScopeClaimOnly
    {
        [Fact]
        public async Task ReadScope_CanRead()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", ReadValue));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WriteScope_CanWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", WriteValue));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Single(oAuth2TestHost.DeadLetters.RequeueCalls);
        }

        [Fact]
        public async Task ReadScope_CannotWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", ReadValue));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(oAuth2TestHost.DeadLetters.RequeueCalls);
        }

        [Fact]
        public async Task UnrelatedScope_CannotRead()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", "Other.Scope"));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UnrelatedScope_CannotWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", "Other.Scope"));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ScopeWithDifferentCasing_IsForbidden()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", ReadValue.ToLowerInvariant()));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SpaceSeparatedScopes_CanReadAndWriteWhenBothAreListed()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", $"openid {ReadValue} {WriteValue}"));

            var readResponse = await oAuth2TestHost.Client.GetAsync(ListPath);
            var writeResponse = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, writeResponse.StatusCode);
        }

        [Fact]
        public async Task SpaceSeparatedScopesWithOnlyReadListed_CannotWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", $"openid profile {ReadValue}"));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ScopeThatOnlyContainsTheRequiredValueAsASubstring_IsForbidden()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("scp", $"{ReadValue}.Extended"));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ScopeWithWrongAudience_IsRejectedAs401()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken("api://another-api", new Claim("scp", ReadValue));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class RolesClaimOnly
    {
        [Fact]
        public async Task ReadRole_CanRead()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", ReadValue));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WriteRole_CanWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", WriteValue));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task ReadRole_CannotWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", ReadValue));

            var response = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UnrelatedRole_CannotRead()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", "Other.Role"));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class RolesAndScopeClaims
    {
        [Fact]
        public async Task ReadScopeAndWriteRole_CanReadAndWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", WriteValue), new Claim("scp", ReadValue));

            var readResponse = await oAuth2TestHost.Client.GetAsync(ListPath);
            var writeResponse = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, writeResponse.StatusCode);
        }

        [Fact]
        public async Task UnrelatedRoleAndReadScope_CanReadButNotWrite()
        {
            using var oAuth2TestHost = new OAuth2TestHost();
            oAuth2TestHost.UseToken(new Claim("roles", "Other.Role"), new Claim("scp", ReadValue));

            var readResponse = await oAuth2TestHost.Client.GetAsync(ListPath);
            var writeResponse = await oAuth2TestHost.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
        }
    }
}
