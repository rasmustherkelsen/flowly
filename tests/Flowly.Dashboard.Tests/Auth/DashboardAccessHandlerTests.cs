using System.Security.Claims;
using Flowly.Dashboard.Auth;
using Flowly.Dashboard.Tests.Fakes;
using Microsoft.AspNetCore.Authorization;

namespace Flowly.Dashboard.Tests.Auth;

public class DashboardAccessHandlerTests
{
    public class HandleRequirementAsync
    {
        [Fact]
        public async Task WithNoRolesOrPolicies_Succeeds()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(new FakeAuthorizationService([])));
            var requirement = new DashboardAccessRequirement(roles: null, policyNames: null);
            var context = CreateContext(requirement, CreateUser());

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task WithMatchingRole_Succeeds()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(new FakeAuthorizationService([])));
            var requirement = new DashboardAccessRequirement(roles: ["admin"], policyNames: null);
            var context = CreateContext(requirement, CreateUser("admin"));

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task WithNonMatchingRole_AndNoPolicies_DoesNotSucceed()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(new FakeAuthorizationService([])));
            var requirement = new DashboardAccessRequirement(roles: ["admin"], policyNames: null);
            var context = CreateContext(requirement, CreateUser("viewer"));

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task WithMatchingNamedPolicy_Succeeds()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(new FakeAuthorizationService(new Dictionary<string, bool> { ["special-access"] = true })));
            var requirement = new DashboardAccessRequirement(roles: null, policyNames: ["special-access"]);
            var context = CreateContext(requirement, CreateUser());

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task WithNonMatchingNamedPolicy_DoesNotSucceed()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(new FakeAuthorizationService(new Dictionary<string, bool> { ["special-access"] = false })));
            var requirement = new DashboardAccessRequirement(roles: null, policyNames: ["special-access"]);
            var context = CreateContext(requirement, CreateUser());

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task WithFirstPolicyFailingAndSecondMatching_Succeeds()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(
                new FakeAuthorizationService(new Dictionary<string, bool> { ["first"] = false, ["second"] = true })));
            var requirement = new DashboardAccessRequirement(roles: null, policyNames: ["first", "second"]);
            var context = CreateContext(requirement, CreateUser());

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task WithNonMatchingRole_ButMatchingPolicy_Succeeds()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(
                new FakeAuthorizationService(new Dictionary<string, bool> { ["fallback-policy"] = true })));
            var requirement = new DashboardAccessRequirement(roles: ["admin"], policyNames: ["fallback-policy"]);
            var context = CreateContext(requirement, CreateUser("viewer"));

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task WithNullHttpContext_AndPolicies_DoesNotSucceed()
        {
            var dashboardAccessHandler = new DashboardAccessHandler(new FakeHttpContextAccessor(authorizationService: null));
            var requirement = new DashboardAccessRequirement(roles: null, policyNames: ["special-access"]);
            var context = CreateContext(requirement, CreateUser());

            await ((IAuthorizationHandler)dashboardAccessHandler).HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        private static AuthorizationHandlerContext CreateContext(DashboardAccessRequirement requirement, ClaimsPrincipal user)
            => new([requirement], user, resource: null);

        private static ClaimsPrincipal CreateUser(params string[] roles)
            => new(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), authenticationType: "TestAuth"));
    }
}
