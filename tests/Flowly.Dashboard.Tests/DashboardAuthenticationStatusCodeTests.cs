using System.Net;
using System.Security.Claims;
using Flowly.Dashboard.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Dashboard.Tests;

public class DashboardAuthenticationStatusCodeTests
{
    public class UnauthenticatedRequest
    {
        [Fact]
        public async Task ToViewerSecuredApiEndpoint_Returns401()
        {
            using var client = CreateClient();

            var response = await client.GetAsync("/flowly/api/config");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ToMutationEndpoint_Returns401()
        {
            using var client = CreateClient();

            var response = await client.PostAsync("/flowly/api/submit", new StringContent(""));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ToSubmittersEndpoint_Returns401()
        {
            using var client = CreateClient();

            var response = await client.GetAsync("/flowly/api/submitters");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    public class AuthenticatedWithoutRequiredRole
    {
        [Fact]
        public async Task ToViewerSecuredApiEndpoint_Returns403()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "someone-else");

            var response = await GetWithCookie(client, "/flowly/api/config", cookie);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ToSubmittersEndpoint_Returns403()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "viewer");

            var response = await GetWithCookie(client, "/flowly/api/submitters", cookie);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    public class AuthenticatedWithRequiredRole
    {
        [Fact]
        public async Task ToViewerSecuredApiEndpoint_Returns200()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "viewer");

            var response = await GetWithCookie(client, "/flowly/api/config", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ToSubmittersEndpoint_Returns200()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "submitter");

            var response = await GetWithCookie(client, "/flowly/api/submitters", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class AuthenticatedViewerWithNoDistinctSubmitterRoleConfigured
    {
        [Fact]
        public async Task ToSubmittersEndpoint_Returns200()
        {
            using var client = CreateClient(new OAuthAuthenticationOptions(
                clientId: "test-client",
                authority: "https://example-authority.test/",
                viewerRoles: ["viewer"]));
            var cookie = await SignIn(client, role: "viewer");

            var response = await GetWithCookie(client, "/flowly/api/submitters", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class AuthenticatedWithoutRequiredViewerRole
    {
        [Fact]
        public async Task ToDashboardShell_Returns403WithoutRedirect()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "someone-else");

            var response = await GetWithCookie(client, "/flowly/some-page", cookie);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }
    }

    public class AuthenticatedWithRequiredViewerRole
    {
        [Fact]
        public async Task ToDashboardShell_Returns200()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "viewer");

            var response = await GetWithCookie(client, "/flowly/some-page", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class AuthenticatedViewerWithoutSubmitterRole
    {
        [Fact]
        public async Task ToMutationEndpoint_Returns403()
        {
            using var client = CreateClient();
            var cookie = await SignIn(client, role: "viewer");

            var request = new HttpRequestMessage(HttpMethod.Post, "/flowly/api/submit") { Content = new StringContent("") };
            request.Headers.Add("Cookie", cookie);
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static HttpClient CreateClient(OAuthAuthenticationOptions? authOptions = null)
    {
        authOptions ??= new OAuthAuthenticationOptions(
            clientId: "test-client",
            authority: "https://example-authority.test/",
            viewerRoles: ["viewer"],
            submitterRoles: ["submitter"]);

        var host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    services.AddFlowlyDashboard(options => options.Authentication = authOptions);
                });
                webHost.Configure(app =>
                {
                    app.Map("/test-signin", signIn => signIn.Run(SignInTestUser));
                    app.UseFlowlyDashboard();
                });
            })
            .Start();

        return host.GetTestServer().CreateClient();
    }

    private static async Task SignInTestUser(HttpContext ctx)
    {
        var role = ctx.Request.Query["role"].ToString();
        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };

        if (!string.IsNullOrEmpty(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, DashboardAuthConstants.CookieScheme));
        await ctx.SignInAsync(DashboardAuthConstants.CookieScheme, principal);
    }

    private static async Task<string> SignIn(HttpClient client, string role)
    {
        var response = await client.GetAsync($"/test-signin?role={role}");
        return response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    }

    private static Task<HttpResponseMessage> GetWithCookie(HttpClient client, string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }
}
