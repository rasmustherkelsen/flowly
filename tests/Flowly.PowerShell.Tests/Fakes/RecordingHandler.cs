namespace Flowly.PowerShell.Tests.Fakes;

internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public HttpContent? LastRequestContent { get; private set; }
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestContent = request.Content;
        RequestCount++;
        return Task.FromResult(respond(request));
    }
}
