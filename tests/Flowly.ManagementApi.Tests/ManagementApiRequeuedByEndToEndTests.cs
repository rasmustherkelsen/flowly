using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Flowly.ManagementApi.Tests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Flowly.ManagementApi.Tests;

public class ManagementApiRequeuedByEndToEndTests
{
    private const string RequeuePath = "/flowly/management/dead-letters/requeue";
    private const string ForgedHeaderValue = "forged-by-client";

    public class ApiKeyAuthorization
    {
        [Fact]
        public async Task WithoutARequester_StoresNoRequesterEvenWhenTheClientSendsIdentityHeaders()
        {
            using var hostFixture = new HostFixture(HostMode.ApiKey);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ApiKey);
            AddForgedIdentityHeaders(hostFixture.Client);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(("m1", null), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithAnExplicitRequester_StoresIt()
        {
            using var hostFixture = new HostFixture(HostMode.ApiKey);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ApiKey);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "ops" });

            Assert.Equal(("m1", "ops"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithAMessageBody_PassesItToTheService()
        {
            using var hostFixture = new HostFixture(HostMode.ApiKey);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ApiKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "corrected" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("corrected", Assert.Single(hostFixture.DeadLetters.ReplacementBodies));
        }
    }

    public class OAuth2Authorization
    {
        [Fact]
        public async Task WithAMessageBody_PassesItToTheService()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("sub", "service-principal-1"));

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "corrected" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("corrected", Assert.Single(hostFixture.DeadLetters.ReplacementBodies));
        }

        [Fact]
        public async Task WithoutARequesterAndAClientCredentialsToken_StoresTheSubject()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("sub", "service-principal-1"), new Claim("azp", "client-app-1"));

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(("m1", "service-principal-1"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithoutARequesterAndATokenWithOnlyAnAuthorizedParty_StoresTheAuthorizedParty()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("azp", "client-app-1"));

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(("m1", "client-app-1"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithoutARequesterAndATokenWithAName_StoresTheNameBeforeTheSubject()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("unique_name", "alice@example.com"), new Claim("sub", "subject-1"));

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(("m1", "alice@example.com"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithoutARequesterAndATokenWithoutIdentityClaims_StoresNoRequester()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"));

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(("m1", null), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithAnExplicitRequester_StoresTheTokenIdentityInstead()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("sub", "service-principal-1"));

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "ops" });

            Assert.Equal(("m1", "service-principal-1"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithAnExplicitRequesterAndATokenWithoutIdentityClaims_StoresTheExplicitRequester()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"));

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "ops" });

            Assert.Equal(("m1", "ops"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithoutARequesterAndForgedIdentityHeaders_StoresTheTokenIdentity()
        {
            using var hostFixture = new HostFixture(HostMode.OAuth2);
            hostFixture.UseToken(new Claim("roles", "Flowly.Management.Write"), new Claim("sub", "service-principal-1"));
            AddForgedIdentityHeaders(hostFixture.Client);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(("m1", "service-principal-1"), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }
    }

    public class AuthorizationPolicies
    {
        [Fact]
        public async Task WithAMessageBody_PassesItToTheService()
        {
            using var hostFixture = new HostFixture(HostMode.ExistingPolicies);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "corrected" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("corrected", Assert.Single(hostFixture.DeadLetters.ReplacementBodies));
        }

        [Fact]
        public async Task WithoutARequester_StoresThePrincipalName()
        {
            using var hostFixture = new HostFixture(HostMode.ExistingPolicies);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(("m1", PolicyAuthenticationHandler.UserName), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithABlankRequester_StoresThePrincipalName()
        {
            using var hostFixture = new HostFixture(HostMode.ExistingPolicies);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "  " });

            Assert.Equal(("m1", PolicyAuthenticationHandler.UserName), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithAnExplicitRequester_StoresThePrincipalNameInstead()
        {
            using var hostFixture = new HostFixture(HostMode.ExistingPolicies);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "ops" });

            Assert.Equal(("m1", PolicyAuthenticationHandler.UserName), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }

        [Fact]
        public async Task WithForgedIdentityHeaders_StoresThePrincipalName()
        {
            using var hostFixture = new HostFixture(HostMode.ExistingPolicies);
            AddForgedIdentityHeaders(hostFixture.Client);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(("m1", PolicyAuthenticationHandler.UserName), Assert.Single(hostFixture.DeadLetters.RequeueCalls));
        }
    }

    private static void AddForgedIdentityHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Requeued-By", ForgedHeaderValue);
        client.DefaultRequestHeaders.Add("X-Forwarded-User", ForgedHeaderValue);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ForgedHeaderValue);
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL-NAME", ForgedHeaderValue);
    }

    private enum HostMode
    {
        ApiKey,
        OAuth2,
        ExistingPolicies,
    }

    private sealed class PolicyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "HostScheme";
        public const string UserName = "host-user@example.com";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, UserName)], SchemeName)),
                SchemeName)));
    }

    private sealed class HostFixture : IDisposable
    {
        public const string ApiKey = "test-key";

        private const string Issuer = "https://issuer.test";
        private const string Audience = "api://flowly-management-test";

        private readonly IHost _host;
        private readonly SymmetricSecurityKey _signingKey = new(new byte[32].Select((_, index) => (byte)(index + 1)).ToArray());

        public HttpClient Client { get; }

        public FakeDeadLetterService DeadLetters { get; } = new();

        public HostFixture(HostMode mode)
        {
            DeadLetters.Add("m1");

            _host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.UseDefaultServiceProvider(options => options.ValidateScopes = true);
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        DeadLetters.AddTo(services);
                        ConfigureAuthorization(services, mode);
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.UseFlowlyManagementApi());
                    });
                })
                .Start();

            Client = _host.GetTestServer().CreateClient();
        }

        public void UseToken(params Claim[] claims)
        {
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            });

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
        }

        private void ConfigureAuthorization(IServiceCollection services, HostMode mode)
        {
            switch (mode)
            {
                case HostMode.ApiKey:
                    services.AddFlowlyManagementApi(options => options.RequireApiKey(ApiKey));
                    break;

                case HostMode.OAuth2:
                    services.AddFlowlyManagementApi(options => options.UseOAuth2(oauth =>
                    {
                        oauth.Authority = Issuer;
                        oauth.Audience = Audience;
                    }));
                    services.PostConfigure<JwtBearerOptions>(ManagementApiRegistrationExtensions.OAuthSchemeName, options =>
                    {
                        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                        configuration.SigningKeys.Add(_signingKey);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    });
                    break;

                case HostMode.ExistingPolicies:
                    services.AddAuthentication(PolicyAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, PolicyAuthenticationHandler>(PolicyAuthenticationHandler.SchemeName, _ => { });
                    services.AddAuthorizationBuilder()
                        .AddPolicy("HostRead", policy => policy.RequireAuthenticatedUser())
                        .AddPolicy("HostWrite", policy => policy.RequireAuthenticatedUser());
                    services.AddFlowlyManagementApi(options => options.RequireAuthorizationPolicies("HostRead", "HostWrite"));
                    break;
            }
        }
    }
}
