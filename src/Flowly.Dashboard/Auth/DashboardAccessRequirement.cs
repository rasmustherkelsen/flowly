using Microsoft.AspNetCore.Authorization;

namespace Flowly.Dashboard.Auth;

internal sealed class DashboardAccessRequirement(IReadOnlyList<string>? roles, IReadOnlyList<string>? policyNames) : IAuthorizationRequirement
{
    internal IReadOnlyList<string>? Roles { get; } = roles;
    internal IReadOnlyList<string>? PolicyNames { get; } = policyNames;
}