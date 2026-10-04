using Microsoft.AspNetCore.Authentication;

namespace Flowly.ManagementApi.Authorization;

internal sealed class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string ReadApiKey { get; set; } = "";
    public string WriteApiKey { get; set; } = "";
}
