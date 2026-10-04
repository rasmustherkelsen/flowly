using System.Text.Json.Serialization;

namespace Flowly.PowerShell.Http;

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);
