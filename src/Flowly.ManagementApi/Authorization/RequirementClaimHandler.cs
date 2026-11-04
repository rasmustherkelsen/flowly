using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Flowly.ManagementApi.Authorization;

internal sealed class RequirementClaimHandler : AuthorizationHandler<RequirementClaimRequirement>
{
    private const string RolesClaimType = "roles";
    private const string ScopeClaimType = "scp";
    private const string MappedScopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequirementClaimRequirement requirement)
    {
        var hasAppRole = context.User.Claims.Any(c => IsRoleClaim(c) && c.Value == requirement.Value);
        var hasDelegatedScope = context.User.Claims
            .Where(IsScopeClaim)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(scope => scope == requirement.Value);

        if (hasAppRole || hasDelegatedScope)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static bool IsRoleClaim(Claim claim) => claim.Type is RolesClaimType or ClaimTypes.Role;

    private static bool IsScopeClaim(Claim claim) => claim.Type is ScopeClaimType or MappedScopeClaimType;
}
