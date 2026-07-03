using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Flowly.Dashboard.Tests.Fakes;

internal sealed class FakeAuthorizationService(Dictionary<string, bool> policyResults) : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        => Task.FromResult(AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        => Task.FromResult(policyResults.TryGetValue(policyName, out var succeeded) && succeeded
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed());
}
