using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Dashboard.Auth;

internal sealed class DashboardAccessHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<DashboardAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DashboardAccessRequirement requirement)
    {
        if (requirement.Roles == null && requirement.PolicyNames == null || requirement.Roles?.Any(r => context.User.IsInRole(r)) == true)
        {
            context.Succeed(requirement);
            return;
        }

        if (requirement.PolicyNames is { Count: > 0 })
        {
            var authService = httpContextAccessor.HttpContext?.RequestServices.GetService<IAuthorizationService>();

            if (authService != null)
            {
                foreach (var policyName in requirement.PolicyNames)
                {
                    var result = await authService.AuthorizeAsync(context.User, resource: null, policyName: policyName);

                    if (result.Succeeded)
                    {
                        context.Succeed(requirement);
                        return;
                    }
                }
            }
        }
    }
}