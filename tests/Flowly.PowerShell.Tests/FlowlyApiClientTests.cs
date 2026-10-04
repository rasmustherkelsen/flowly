using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Tests;

public class FlowlyApiClientTests
{
    public class ListCheckpoints
    {
        [Fact]
        public async Task WithNoFilters_RequestsUnfilteredPath()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CheckpointDto>()),
            });
            var connection = CreateApiKeyConnection(handler);

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("/flowly/management/message-streams/checkpoints", handler.LastRequest!.RequestUri!.AbsolutePath);
            Assert.Null(handler.LastRequest.RequestUri.Query is { Length: > 0 } q ? q : null);
        }

        [Fact]
        public async Task WithFilters_IncludesThemInQueryString()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CheckpointDto>()),
            });
            var connection = CreateApiKeyConnection(handler);

            await FlowlyApiClient.ListCheckpoints(connection, "my-queue", "MyHandler", CancellationToken.None);

            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("queue=my-queue", query);
            Assert.Contains("consumer=MyHandler", query);
        }

        [Fact]
        public async Task DeserializesResponseBody()
        {
            var expected = new[] { new CheckpointDto("q", "c", null, 42, DateTimeOffset.UtcNow) };
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });
            var connection = CreateApiKeyConnection(handler);

            var result = await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal(42, Assert.Single(result).Position);
        }

        [Fact]
        public async Task IncludesApiKeyHeader()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<CheckpointDto>()),
            });
            var connection = CreateApiKeyConnection(handler);

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("the-api-key", handler.LastRequest!.Headers.GetValues("X-Flowly-Api-Key").Single());
        }

        [Fact]
        public async Task OnErrorStatusCode_Throws()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = CreateApiKeyConnection(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None));
        }
    }

    public class ResetCheckpoint
    {
        [Fact]
        public async Task PostsToResetPathWithJsonBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = CreateApiKeyConnection(handler);

            await FlowlyApiClient.ResetCheckpoint(connection, "my-queue", "MyHandler", 2, 99, CancellationToken.None);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/message-streams/checkpoints/reset", handler.LastRequest.RequestUri!.AbsolutePath);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal("my-queue", body.RootElement.GetProperty("queueName").GetString());
            Assert.Equal("MyHandler", body.RootElement.GetProperty("consumerName").GetString());
            Assert.Equal(2, body.RootElement.GetProperty("partition").GetInt32());
            Assert.Equal(99, body.RootElement.GetProperty("offset").GetInt64());
        }

        [Fact]
        public async Task OnErrorStatusCode_Throws()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = CreateApiKeyConnection(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ResetCheckpoint(connection, "q", "c", null, 1, CancellationToken.None));
        }
    }

    private static FlowlyConnection CreateApiKeyConnection(HttpMessageHandler handler)
    {
        var baseUri = new Uri("https://example.test/flowly/management/");
        var httpClient = new HttpClient(handler) { BaseAddress = baseUri };
        httpClient.DefaultRequestHeaders.Add("X-Flowly-Api-Key", "the-api-key");
        return new FlowlyConnection { BaseUri = baseUri, HttpClient = httpClient };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpContent? LastRequestContent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestContent = request.Content;
            return Task.FromResult(respond(request));
        }
    }
}
