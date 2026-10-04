using Microsoft.AspNetCore.Authorization;

namespace Flowly.ManagementApi.Authorization;

internal sealed class RequirementClaimHandler : AuthorizationHandler<RequirementClaimRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequirementClaimRequirement requirement)
    {
        var hasAppRole = context.User.Claims.Any(c => c.Type is "roles" or System.Security.Claims.ClaimTypes.Role && c.Value == requirement.Value);
        var hasDelegatedScope = context.User.Claims
            .Where(c => c.Type == "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(scope => scope == requirement.Value);

        if (hasAppRole || hasDelegatedScope)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
