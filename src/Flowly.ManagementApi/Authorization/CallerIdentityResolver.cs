using System.Security.Claims;

namespace Flowly.ManagementApi.Authorization;

/// <summary>
///     Resolves the identity of the authenticated caller of a Management API request for audit trails, from the
///     principal the host's authentication produced and never from request headers or bodies, so a client cannot
///     choose the value it is recorded under.
/// </summary>
internal static class CallerIdentityResolver
{
    /// <summary>The longest identity returned, matching the column the dead letter audit field is stored in.</summary>
    internal const int MaxLength = 200;

    private const string SubjectClaimType = "sub";
    private const string AuthorizedPartyClaimType = "azp";

    /// <summary>
    ///     Resolves the caller's identity according to how the request was authorized. An API key identifies no one, so
    ///     the result is always <see langword="null" /> in that mode. For host authorization policies it is the name of
    ///     the principal's identity (<see cref="System.Security.Principal.IIdentity.Name" />). For OAuth2 it is that name
    ///     when the token yields one, then the subject (<c>sub</c>, or the <see cref="ClaimTypes.NameIdentifier" /> claim
    ///     the JWT handler maps it to), then the authorized party (<c>azp</c>, the client application id of a client
    ///     credentials token).
    /// </summary>
    /// <param name="user">The principal of the request, which may be unauthenticated.</param>
    /// <param name="authMode">How the Management API authorizes its callers.</param>
    /// <returns>
    ///     The caller's identity, truncated to <see cref="MaxLength" /> characters, or <see langword="null" /> when the
    ///     mode or the principal carries no usable name.
    /// </returns>
    public static string? Resolve(ClaimsPrincipal? user, ManagementApiAuthMode authMode)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var identity = authMode switch
        {
            ManagementApiAuthMode.ExistingPolicies => user.Identity.Name,
            ManagementApiAuthMode.OAuth2 => FirstNonBlank(
                user.Identity.Name,
                user.FindFirst(SubjectClaimType)?.Value,
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                user.FindFirst(AuthorizedPartyClaimType)?.Value),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(identity) ? null : Truncate(identity);
    }

    private static string? FirstNonBlank(params string?[] candidates) => candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

    private static string Truncate(string identity) => identity.Length <= MaxLength ? identity : identity[..MaxLength];
}
