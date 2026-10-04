using Microsoft.AspNetCore.Authorization;

namespace Flowly.ManagementApi.Authorization;

/// <summary>
///     Satisfied when the current user carries <see cref="Value" /> in either the <c>roles</c> claim (Entra App
///     Roles — app-only, client-credentials tokens) or the <c>scp</c> claim (delegated OAuth scopes — interactive
///     user sign-in tokens), so <see cref="ManagementApiOAuthOptions.ReadRequirement" />/<see cref="ManagementApiOAuthOptions.WriteRequirement" />
///     work regardless of which flow issued the token.
/// </summary>
internal sealed class RequirementClaimRequirement(string value) : IAuthorizationRequirement
{
    public string Value { get; } = value;
}
