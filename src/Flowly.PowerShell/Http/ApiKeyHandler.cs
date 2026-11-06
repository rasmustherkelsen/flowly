using System.Security;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Http;

internal sealed class ApiKeyHandler(SecureString apiKey, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    public const string HeaderName = "X-Flowly-Api-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, apiKey.ToPlainText());

        return base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            apiKey.Dispose();
        }

        base.Dispose(disposing);
    }
}
