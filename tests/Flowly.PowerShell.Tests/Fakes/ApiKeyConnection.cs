using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Tests.Fakes;

internal static class ApiKeyConnection
{
    public static FlowlyConnection Create(HttpMessageHandler handler)
    {
        var baseUri = new Uri("https://example.test/flowly/management/");
        var httpClient = new HttpClient(handler) { BaseAddress = baseUri };
        httpClient.DefaultRequestHeaders.Add("X-Flowly-Api-Key", "the-api-key");

        return new FlowlyConnection { BaseUri = baseUri, HttpClient = httpClient };
    }
}
