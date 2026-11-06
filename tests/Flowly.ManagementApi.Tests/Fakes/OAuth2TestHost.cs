using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Flowly.ManagementApi.Tests.Fakes;

internal sealed class OAuth2TestHost : IDisposable
{
    public const string Issuer = "https://issuer.test";
    public const string Audience = "api://flowly-management-test";

    private readonly IHost _host;
    private readonly SymmetricSecurityKey _signingKey = new(new byte[32].Select((_, index) => (byte)(index + 1)).ToArray());

    public HttpClient Client { get; }

    public FakeDeadLetterService DeadLetters { get; } = new();

    public OAuth2TestHost(Action<IServiceCollection>? configureHostServices = null)
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
                    configureHostServices?.Invoke(services);
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

    public void UseToken(params Claim[] claims) => UseToken(Audience, claims);

    public void UseToken(string audience, params Claim[] claims)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
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
}
