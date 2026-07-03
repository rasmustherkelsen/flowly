using Flowly.Dashboard.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Dashboard.Tests;

public class UseFlowlyDashboardPolicyValidationTests
{
    public class WithUnregisteredViewerPolicy
    {
        [Fact]
        public void Throws()
        {
            var app = BuildApp(options => options.Authentication = new OAuthAuthenticationOptions(
                clientId: "client",
                authority: "https://accounts.google.com",
                viewerPolicies: ["nonexistent"]));

            var invalidOperationException = Assert.Throws<InvalidOperationException>(() => app.UseFlowlyDashboard());

            Assert.Contains("nonexistent", invalidOperationException.Message);
        }
    }

    public class WithUnregisteredSubmitterPolicy
    {
        [Fact]
        public void Throws()
        {
            var app = BuildApp(options => options.Authentication = new OAuthAuthenticationOptions(
                clientId: "client",
                authority: "https://accounts.google.com",
                submitterPolicies: ["nonexistent"]));

            var invalidOperationException = Assert.Throws<InvalidOperationException>(() => app.UseFlowlyDashboard());

            Assert.Contains("nonexistent", invalidOperationException.Message);
        }
    }

    public class WithAllPoliciesRegistered
    {
        [Fact]
        public void DoesNotThrow()
        {
            var app = BuildApp(
                options => options.Authentication = new OAuthAuthenticationOptions(
                    clientId: "client",
                    authority: "https://accounts.google.com",
                    viewerPolicies: ["registered-policy"]),
                services => services.AddAuthorizationBuilder().AddPolicy("registered-policy", policy => policy.RequireAuthenticatedUser()));

            app.UseFlowlyDashboard();
        }
    }

    public class WithRolesOnlyAndNoPolicies
    {
        [Fact]
        public void DoesNotThrow()
        {
            var app = BuildApp(options => options.Authentication = new OAuthAuthenticationOptions(
                clientId: "client",
                authority: "https://accounts.google.com",
                viewerRoles: ["admin"]));

            app.UseFlowlyDashboard();
        }
    }

    public class WithoutAuthentication
    {
        [Fact]
        public void DoesNotThrow()
        {
            var app = BuildApp(options => options.Authentication = null);

            app.UseFlowlyDashboard();
        }
    }

    private static WebApplication BuildApp(Action<FlowlyDashboardOptions> configureDashboard, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddFlowlyDashboard(configureDashboard);
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }
}
