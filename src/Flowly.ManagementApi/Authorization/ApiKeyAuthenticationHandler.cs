using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flowly.ManagementApi.Authorization;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "FlowlyManagementApiKey";
    public const string HeaderName = "X-Flowly-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedKey) || providedKey.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail($"Missing {HeaderName} header."));

        var key = providedKey.ToString();
        var claims = new List<Claim>();

        if (key == Options.ReadApiKey)
            claims.Add(new Claim(ClaimTypes.Role, "Flowly.Management.Read"));

        if (key == Options.WriteApiKey)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Flowly.Management.Read"));
            claims.Add(new Claim(ClaimTypes.Role, "Flowly.Management.Write"));
        }

        if (claims.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity(claims.Distinct(), SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
