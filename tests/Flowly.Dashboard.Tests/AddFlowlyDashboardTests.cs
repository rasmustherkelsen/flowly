using Flowly.Dashboard.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Flowly.Dashboard.Tests;

public class AddFlowlyDashboardTests
{
    public class WithAuthentication
    {
        [Fact]
        public async Task RegistersCookieDashboardScheme()
        {
            var services = BuildAuthenticatedServices();

            var scheme = await services.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("FlowlyDashboard.Cookies");

            Assert.NotNull(scheme);
        }

        [Fact]
        public async Task RegistersOidcDashboardScheme()
        {
            var services = BuildAuthenticatedServices();

            var scheme = await services.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("FlowlyDashboard.Oidc");

            Assert.NotNull(scheme);
        }

        [Fact]
        public void RegistersViewerAuthorizationPolicy()
        {
            var services = BuildAuthenticatedServices();

            var policy = services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy("FlowlyDashboard.Viewer");

            Assert.NotNull(policy);
        }

        [Fact]
        public void RegistersSubmitterAuthorizationPolicy()
        {
            var services = BuildAuthenticatedServices();

            var policy = services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy("FlowlyDashboard.Submitter");

            Assert.NotNull(policy);
        }

        [Fact]
        public void ConfiguresOidcWithQueryResponseMode()
        {
            var services = BuildAuthenticatedServices();

            var oidcOptions = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("FlowlyDashboard.Oidc");

            Assert.Equal(OpenIdConnectResponseMode.Query, oidcOptions.ResponseMode);
        }

        [Fact]
        public void RegistersDashboardAccessHandler()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();

            serviceCollection.AddFlowlyDashboard(options => options.Authentication = new OAuthAuthenticationOptions("client", "https://accounts.google.com"));

            Assert.Contains(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IAuthorizationHandler) && descriptor.ImplementationType == typeof(DashboardAccessHandler));
        }

        private static ServiceProvider BuildAuthenticatedServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();

            serviceCollection.AddFlowlyDashboard(options => options.Authentication = new OAuthAuthenticationOptions("client", "https://accounts.google.com"));

            return serviceCollection.BuildServiceProvider();
        }
    }

    public class WithoutAuthentication
    {
        [Fact]
        public void DoesNotRegisterDashboardAccessHandler()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();

            serviceCollection.AddFlowlyDashboard();

            Assert.DoesNotContain(serviceCollection, descriptor =>
                descriptor.ServiceType == typeof(IAuthorizationHandler) && descriptor.ImplementationType == typeof(DashboardAccessHandler));
        }
    }
}
