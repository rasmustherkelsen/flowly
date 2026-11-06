using System.Net;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;

namespace Flowly.PowerShell.Tests;

public class ApiKeyHandlerTests
{
    public class SendAsync
    {
        [Fact]
        public async Task AddsTheRevealedApiKeyHeader()
        {
            var inner = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var invoker = new HttpMessageInvoker(new ApiKeyHandler(new NetworkCredential(string.Empty, "the-api-key").SecurePassword, inner));

            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/"), CancellationToken.None);

            Assert.Equal("the-api-key", inner.LastRequest!.Headers.GetValues(ApiKeyHandler.HeaderName).Single());
        }

        [Fact]
        public async Task SendingTwiceWithTheSameRequest_KeepsASingleHeaderValue()
        {
            var inner = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var invoker = new HttpMessageInvoker(new ApiKeyHandler(new NetworkCredential(string.Empty, "the-api-key").SecurePassword, inner));
            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

            await invoker.SendAsync(request, CancellationToken.None);
            await invoker.SendAsync(request, CancellationToken.None);

            Assert.Single(inner.LastRequest!.Headers.GetValues(ApiKeyHandler.HeaderName));
        }
    }
}
