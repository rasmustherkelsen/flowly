using System.Management.Automation;
using System.Net.Http.Headers;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Cmdlets;

/// <summary>Establishes the connection used by every other Flowly cmdlet in this session.</summary>
[Cmdlet(VerbsCommunications.Connect, "Flowly")]
public sealed class ConnectFlowlyCommand : PSCmdlet
{
    /// <summary>The base URL of the Flowly Management API, e.g. <c>https://myapp.example.com/flowly/management</c>.</summary>
    [Parameter(Mandatory = true)]
    public string BaseUri { get; set; } = null!;

    /// <summary>An API key presented via the <c>X-Flowly-Api-Key</c> header. Mutually exclusive with the OAuth2 parameters.</summary>
    [Parameter(ParameterSetName = "ApiKey", Mandatory = true)]
    public string? ApiKey { get; set; }

    /// <summary>The Entra ID tenant ID, for client-credentials authentication.</summary>
    [Parameter(ParameterSetName = "OAuth2", Mandatory = true)]
    public string? TenantId { get; set; }

    /// <summary>The Entra ID app registration's client ID, for client-credentials authentication.</summary>
    [Parameter(ParameterSetName = "OAuth2", Mandatory = true)]
    public string? ClientId { get; set; }

    /// <summary>The Entra ID app registration's client secret, for client-credentials authentication.</summary>
    [Parameter(ParameterSetName = "OAuth2", Mandatory = true)]
    public string? ClientSecret { get; set; }

    /// <summary>The OAuth2 scope to request, e.g. <c>api://flowly-management/.default</c>.</summary>
    [Parameter(ParameterSetName = "OAuth2", Mandatory = true)]
    public string? Scope { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord()
    {
        var baseUri = new Uri(BaseUri.TrimEnd('/') + "/");
        var httpClient = new HttpClient { BaseAddress = baseUri };

        FlowlyConnection connection;

        if (ApiKey is not null)
        {
            httpClient.DefaultRequestHeaders.Add("X-Flowly-Api-Key", ApiKey);
            connection = new FlowlyConnection { BaseUri = baseUri, HttpClient = httpClient };
        }
        else
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            connection = new FlowlyConnection
            {
                BaseUri = baseUri,
                HttpClient = httpClient,
                TenantId = TenantId,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                Scope = Scope,
            };
        }

        FlowlySession.Current?.Dispose();
        FlowlySession.Current = connection;

        WriteObject($"Connected to {baseUri}");
    }
}
