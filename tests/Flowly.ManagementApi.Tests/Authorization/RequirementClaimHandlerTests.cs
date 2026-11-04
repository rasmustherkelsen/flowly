using System.Security.Claims;
using Flowly.ManagementApi.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Flowly.ManagementApi.Tests.Authorization;

public class RequirementClaimHandlerTests
{
    private const string RequiredValue = "Flowly.Management.Read";
    private const string MappedScopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";

    public class HandleRequirement
    {
        [Theory]
        [InlineData("roles")]
        [InlineData(ClaimTypes.Role)]
        public async Task RoleClaimWithRequiredValue_Succeeds(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, RequiredValue));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Theory]
        [InlineData("scp")]
        [InlineData(MappedScopeClaimType)]
        public async Task ScopeClaimWithRequiredValue_Succeeds(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, RequiredValue));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Theory]
        [InlineData("scp")]
        [InlineData(MappedScopeClaimType)]
        public async Task SpaceSeparatedScopeClaimContainingRequiredValue_Succeeds(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, $"openid {RequiredValue}  profile"));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Theory]
        [InlineData("scp")]
        [InlineData(MappedScopeClaimType)]
        public async Task ScopeClaimsSplitAcrossSeveralClaims_SucceedsWhenAnyContainsRequiredValue(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, "openid"), new Claim(claimType, RequiredValue));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.True(context.HasSucceeded);
        }

        [Theory]
        [InlineData("scp")]
        [InlineData(MappedScopeClaimType)]
        public async Task ScopeClaimWithOtherValue_DoesNotSucceed(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, "Flowly.Management.Write openid"));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Theory]
        [InlineData("scp")]
        [InlineData(MappedScopeClaimType)]
        public async Task ScopeClaimWithRequiredValueInDifferentCasing_DoesNotSucceed(string claimType)
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim(claimType, RequiredValue.ToUpperInvariant()));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task RoleClaimWithOtherValue_DoesNotSucceed()
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim("roles", "Flowly.Management.Write"));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task RequiredValueInUnrelatedClaimType_DoesNotSucceed()
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement, new Claim("groups", RequiredValue), new Claim("scope", RequiredValue));
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }

        [Fact]
        public async Task NoClaims_DoesNotSucceed()
        {
            var requirement = new RequirementClaimRequirement(RequiredValue);
            var context = CreateContext(requirement);
            var requirementClaimHandler = new RequirementClaimHandler();

            await requirementClaimHandler.HandleAsync(context);

            Assert.False(context.HasSucceeded);
        }
    }

    private static AuthorizationHandlerContext CreateContext(RequirementClaimRequirement requirement, params Claim[] claims)
        => new([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
}
