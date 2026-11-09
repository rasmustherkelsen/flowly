using System.Net;
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

namespace Flowly.ManagementApi.Tests;

public class ManagementApiRegistrationExtensionsTests
{
    private const string ListPath = "/flowly/management/dead-letters";
    private const string HostScheme = "HostScheme";

    public class AddFlowlyManagementApi
    {
        [Fact]
        public async Task ApiKeyWithHostDefaultScheme_AuthenticatesManagementRequestsWithApiKey()
        {
            using var host = StartHost(options => options.RequireApiKey("key"), AddRejectingHostDefaultScheme);
            host.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", "key");

            var response = await host.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void ApiKeyWithHostDefaultScheme_KeepsHostDefaultScheme()
        {
            using var host = StartHost(options => options.RequireApiKey("key"), AddRejectingHostDefaultScheme);

            var authenticationOptions = host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            Assert.Equal(HostScheme, authenticationOptions.DefaultScheme);
        }

        [Fact]
        public void ApiKeyWithoutHostAuthentication_SetsNoDefaultScheme()
        {
            using var host = StartHost(options => options.RequireApiKey("key"), _ => { });

            var authenticationOptions = host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            Assert.Null(authenticationOptions.DefaultScheme);
        }

        [Fact]
        public void OAuth2WithHostBearerScheme_RegistersBothSchemes()
        {
            using var host = StartHost(
                options => options.UseOAuth2(oauth =>
                {
                    oauth.Authority = OAuth2TestHost.Issuer;
                    oauth.Audience = OAuth2TestHost.Audience;
                }),
                services => services.AddAuthentication().AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { }));

            var authenticationOptions = host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            Assert.Contains(authenticationOptions.Schemes, scheme => scheme.Name == JwtBearerDefaults.AuthenticationScheme);
            Assert.Contains(authenticationOptions.Schemes, scheme => scheme.Name == ManagementApiRegistrationExtensions.OAuthSchemeName);
        }

        [Fact]
        public void OAuth2_SetsNoDefaultScheme()
        {
            using var host = StartHost(
                options => options.UseOAuth2(oauth =>
                {
                    oauth.Authority = OAuth2TestHost.Issuer;
                    oauth.Audience = OAuth2TestHost.Audience;
                }),
                _ => { });

            var authenticationOptions = host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            Assert.Null(authenticationOptions.DefaultScheme);
        }

        [Fact]
        public async Task OAuth2WithHostDefaultScheme_AuthenticatesManagementRequestsWithBearerToken()
        {
            using var oAuth2TestHost = new OAuth2TestHost(AddRejectingHostDefaultScheme);
            oAuth2TestHost.UseToken(new Claim("scp", "Flowly.Management.Read"));

            var response = await oAuth2TestHost.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static void AddRejectingHostDefaultScheme(IServiceCollection services) =>
        services.AddAuthentication(HostScheme).AddScheme<AuthenticationSchemeOptions, RejectingHandler>(HostScheme, _ => { });

    private static HostWithClient StartHost(Action<ManagementApiOptions> configure, Action<IServiceCollection> configureHostServices)
    {
        var deadLetters = new FakeDeadLetterService();

        var host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    deadLetters.AddTo(services);
                    configureHostServices(services);
                    services.AddFlowlyManagementApi(configure);
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

        return new HostWithClient(host);
    }

    private sealed class RejectingHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.Fail("Host scheme does not accept management requests."));
    }

    private sealed class HostWithClient : IDisposable
    {
        private readonly IHost _host;

        public HostWithClient(IHost host)
        {
            _host = host;
            Client = host.GetTestServer().CreateClient();
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => _host.Services;

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
        }
    }
}
