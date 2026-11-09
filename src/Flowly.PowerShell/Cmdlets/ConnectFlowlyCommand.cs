using System.Management.Automation;
using System.Net.Http.Headers;
using System.Security;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>
///     Establishes the connection used by every other Flowly cmdlet in this session. Authenticates with an API key
///     (<c>-ApiKey</c>), or with OAuth2 client-credentials against an Entra ID tenant (<c>-TenantId</c>, optionally <c>-Authority</c>) or
///     against any other identity provider's token endpoint (<c>-TokenEndpoint</c>). The API key and the client secret are
///     <see cref="SecureString" /> values, kept as such and revealed only while a request is built.
/// </summary>
[Cmdlet(VerbsCommunications.Connect, "Flowly")]
public sealed class ConnectFlowlyCommand : PSCmdlet
{
    private const string ApiKeySet = "ApiKey";
    private const string OAuth2Set = "OAuth2";
    private const string TokenEndpointSet = "OAuth2TokenEndpoint";

    /// <summary>The base URL of the Flowly Management API, e.g. <c>https://myapp.example.com/flowly/management</c>.</summary>
    [Parameter(Mandatory = true)]
    public string BaseUri { get; set; } = null!;

    /// <summary>An API key presented via the <c>X-Flowly-Api-Key</c> header. Mutually exclusive with the OAuth2 parameters.</summary>
    [Parameter(ParameterSetName = ApiKeySet, Mandatory = true)]
    public SecureString? ApiKey { get; set; }

    /// <summary>The Entra ID tenant ID, for client-credentials authentication. Not used with <see cref="TokenEndpoint" />.</summary>
    [Parameter(ParameterSetName = OAuth2Set, Mandatory = true)]
    public string? TenantId { get; set; }

    /// <summary>The app registration's client ID, for client-credentials authentication.</summary>
    [Parameter(ParameterSetName = OAuth2Set, Mandatory = true)]
    [Parameter(ParameterSetName = TokenEndpointSet, Mandatory = true)]
    public string? ClientId { get; set; }

    /// <summary>The app registration's client secret, for client-credentials authentication.</summary>
    [Parameter(ParameterSetName = OAuth2Set, Mandatory = true)]
    [Parameter(ParameterSetName = TokenEndpointSet, Mandatory = true)]
    public SecureString? ClientSecret { get; set; }

    /// <summary>The OAuth2 scope to request, e.g. <c>api://flowly-management/.default</c>.</summary>
    [Parameter(ParameterSetName = OAuth2Set, Mandatory = true)]
    [Parameter(ParameterSetName = TokenEndpointSet, Mandatory = true)]
    public string? Scope { get; set; }

    /// <summary>
    ///     The base URL of the authority the client-credentials token is requested from; the token endpoint is
    ///     <c>{Authority}/{TenantId}/oauth2/v2.0/token</c>. Defaults to <c>https://login.microsoftonline.com</c>; set it for a sovereign cloud such as <c>https://login.microsoftonline.us</c>.
    ///     Mutually exclusive with <see cref="TokenEndpoint" />.
    /// </summary>
    [Parameter(ParameterSetName = OAuth2Set)]
    public string? Authority { get; set; }

    /// <summary>
    ///     The complete token endpoint URL (absolute https), replacing <c>{Authority}/{TenantId}/oauth2/v2.0/token</c>, for identity providers
    ///     other than Entra ID. Mutually exclusive with <see cref="TenantId" /> and <see cref="Authority" />.
    /// </summary>
    [Parameter(ParameterSetName = TokenEndpointSet, Mandatory = true)]
    public string? TokenEndpoint { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        RequireHttpsUrl(Authority, nameof(Authority));
        RequireHttpsUrl(TokenEndpoint, nameof(TokenEndpoint));
        RequireNotEmpty(ApiKey, nameof(ApiKey));
        RequireNotEmpty(ClientSecret, nameof(ClientSecret));

        var baseUri = new Uri(BaseUri.TrimEnd('/') + "/");

        var connection = ApiKey is not null ? CreateApiKeyConnection(baseUri, ApiKey) : CreateOAuth2Connection(baseUri);

        FlowlySession.Current?.Dispose();
        FlowlySession.Current = connection;

        WriteObject($"Connected to {baseUri}");
    }

    private static FlowlyConnection CreateApiKeyConnection(Uri baseUri, SecureString apiKey) => new()
    {
        BaseUri = baseUri,
        HttpClient = new HttpClient(new ApiKeyHandler(apiKey.ToReadOnlyCopy(), new HttpClientHandler())) { BaseAddress = baseUri },
    };

    private FlowlyConnection CreateOAuth2Connection(Uri baseUri)
    {
        var httpClient = new HttpClient { BaseAddress = baseUri };
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return new FlowlyConnection
        {
            BaseUri = baseUri,
            HttpClient = httpClient,
            TenantId = TenantId,
            ClientId = ClientId,
            ClientSecret = ClientSecret!.ToReadOnlyCopy(),
            Scope = Scope,
            Authority = Authority ?? FlowlyConnection.DefaultAuthority,
            TokenEndpoint = TokenEndpoint,
        };
    }

    private void RequireHttpsUrl(string? url, string parameterName)
    {
        if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            ThrowInvalidArgument($"{parameterName} '{url}' is not an absolute https URL.", parameterName);
        }
    }

    private void RequireNotEmpty(SecureString? secret, string parameterName)
    {
        if (secret is { Length: 0 })
        {
            ThrowInvalidArgument($"{parameterName} must not be empty.", parameterName);
        }
    }

    private void ThrowInvalidArgument(string message, string parameterName)
        => ThrowTerminatingError(new ErrorRecord(new ArgumentException(message, parameterName), "InvalidArgument", ErrorCategory.InvalidArgument, parameterName));
}
