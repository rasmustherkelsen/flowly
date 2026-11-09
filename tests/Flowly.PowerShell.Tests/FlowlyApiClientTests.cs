using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.PowerShell.Http;
using Flowly.PowerShell.Tests.Fakes;
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
            var connection = ApiKeyConnection.Create(handler);

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
            var connection = ApiKeyConnection.Create(handler);

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
            var connection = ApiKeyConnection.Create(handler);

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
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("the-api-key", handler.LastRequest!.Headers.GetValues("X-Flowly-Api-Key").Single());
        }

        [Fact]
        public async Task OnErrorStatusCode_Throws()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = ApiKeyConnection.Create(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":\"No checkpoint store is configured.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Contains("No checkpoint store is configured.", exception.Message);
        }
    }

    public class ResetCheckpoint
    {
        [Fact]
        public async Task PostsToResetPathWithJsonBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

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
            var connection = ApiKeyConnection.Create(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ResetCheckpoint(connection, "q", "c", null, 1, CancellationToken.None));
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"detail\":\"The checkpoint cannot be reset while the handler runs.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ResetCheckpoint(connection, "q", "c", null, 1, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
            Assert.Contains("cannot be reset", exception.Message);
        }
    }

    public class TokenAcquisition
    {
        [Fact]
        public async Task WithDefaultAuthority_RequestsTheMicrosoftTokenEndpoint()
        {
            var tokenHandler = new RecordingHandler(_ => TokenResponse());
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()));

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("https://login.microsoftonline.com/the-tenant/oauth2/v2.0/token", tokenHandler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task WithCustomAuthority_RequestsTheTokenEndpointUnderIt()
        {
            var tokenHandler = new RecordingHandler(_ => TokenResponse());
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()), "https://login.microsoftonline.us/");

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("https://login.microsoftonline.us/the-tenant/oauth2/v2.0/token", tokenHandler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task OnTokenErrorResponse_ThrowsWithTheErrorDescriptionAndStatusCode()
        {
            var tokenHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_client\",\"error_description\":\"The client secret is wrong.\"}", System.Text.Encoding.UTF8, "application/json")
            });
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()));

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
            Assert.Contains("invalid_client: The client secret is wrong.", exception.Message);
        }

        [Fact]
        public async Task OnTokenErrorResponseWithoutBody_ThrowsWithTheStatusCode()
        {
            var tokenHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()));

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
            Assert.Contains("502", exception.Message);
        }

        [Fact]
        public async Task WithTokenEndpoint_RequestsItInsteadOfTheAuthorityAndTenantUrl()
        {
            var tokenHandler = new RecordingHandler(_ => TokenResponse());
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()), tokenEndpoint: "https://idp.example.test/connect/token", tenantId: null);

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("https://idp.example.test/connect/token", tokenHandler.LastRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task SendsTheRevealedClientSecretInTheForm()
        {
            var tokenHandler = new RecordingHandler(_ => TokenResponse());
            var connection = OAuth2Connection(tokenHandler, new RecordingHandler(_ => EmptyCheckpoints()));

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            var form = await tokenHandler.LastRequestContent!.ReadAsStringAsync();
            Assert.Contains("client_secret=the-secret", form);
        }

        [Fact]
        public async Task SendsTheAcquiredTokenAsBearerToTheApi()
        {
            var apiHandler = new RecordingHandler(_ => EmptyCheckpoints());
            var connection = OAuth2Connection(new RecordingHandler(_ => TokenResponse()), apiHandler);

            await FlowlyApiClient.ListCheckpoints(connection, null, null, CancellationToken.None);

            Assert.Equal("Bearer the-token", apiHandler.LastRequest!.Headers.Authorization!.ToString());
        }

        private static FlowlyConnection OAuth2Connection(
            HttpMessageHandler tokenHandler,
            HttpMessageHandler apiHandler,
            string? authority = null,
            string? tokenEndpoint = null,
            string? tenantId = "the-tenant")
        {
            var baseUri = new Uri("https://example.test/flowly/management/");

            return new FlowlyConnection
            {
                BaseUri = baseUri,
                HttpClient = new HttpClient(apiHandler) { BaseAddress = baseUri },
                TenantId = tenantId,
                ClientId = "the-client",
                ClientSecret = new NetworkCredential(string.Empty, "the-secret").SecurePassword,
                Scope = "api://flowly/.default",
                Authority = authority ?? FlowlyConnection.DefaultAuthority,
                TokenEndpoint = tokenEndpoint,
                TokenMessageHandler = tokenHandler,
            };
        }

        private static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"the-token\",\"expires_in\":3600}", System.Text.Encoding.UTF8, "application/json")
        };

        private static HttpResponseMessage EmptyCheckpoints() => new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<CheckpointDto>()) };
    }

    public class ListStreamPartitions
    {
        [Fact]
        public async Task WithNoFilters_RequestsUnfilteredPath()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<StreamPartitionDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListStreamPartitions(connection, null, null, null, null, CancellationToken.None);

            Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/message-streams/partitions", handler.LastRequest.RequestUri!.AbsolutePath);
            Assert.Equal("", handler.LastRequest.RequestUri.Query);
        }

        [Fact]
        public async Task WithFilters_IncludesThemInQueryString()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<StreamPartitionDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListStreamPartitions(connection, "my-queue", "MyHandler", 3, "Halted", CancellationToken.None);

            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("queue=my-queue", query);
            Assert.Contains("consumer=MyHandler", query);
            Assert.Contains("partition=3", query);
            Assert.Contains("state=Halted", query);
        }

        [Fact]
        public async Task DeserializesResponseBodyIncludingTheLastError()
        {
            var changedAt = new DateTimeOffset(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);
            var expected = new[]
            {
                new StreamPartitionDto("MyHandler", "q", "c", 2, "Halted", changedAt, new StreamPartitionErrorDto("poison", "System.InvalidOperationException", changedAt)),
                new StreamPartitionDto("MyHandler", "q", "c", null, "Running", changedAt, null)
            };
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });
            var connection = ApiKeyConnection.Create(handler);

            var result = await FlowlyApiClient.ListStreamPartitions(connection, null, null, null, null, CancellationToken.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IncludesApiKeyHeader()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<StreamPartitionDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListStreamPartitions(connection, null, null, null, null, CancellationToken.None);

            Assert.Equal("the-api-key", handler.LastRequest!.Headers.GetValues("X-Flowly-Api-Key").Single());
        }

        [Fact]
        public async Task OnErrorStatusCode_ThrowsWithTheStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListStreamPartitions(connection, null, null, null, null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        }
    }

    public class ResumeStreamPartition
    {
        [Fact]
        public async Task PostsToResumePathWithJsonBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ResumeStreamPartition(connection, "my-queue", "MyHandler", 2, CancellationToken.None);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/message-streams/partitions/resume", handler.LastRequest.RequestUri!.AbsolutePath);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal("my-queue", body.RootElement.GetProperty("queueName").GetString());
            Assert.Equal("MyHandler", body.RootElement.GetProperty("consumerName").GetString());
            Assert.Equal(2, body.RootElement.GetProperty("partition").GetInt32());
        }

        [Fact]
        public async Task ForANonPartitionedStream_SendsANullPartition()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ResumeStreamPartition(connection, "q", "c", null, CancellationToken.None);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("partition").ValueKind);
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"title\":\"Partition is not halted\",\"detail\":\"partition 0 is Running, not Halted\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ResumeStreamPartition(connection, "q", "c", 0, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
            Assert.Contains("partition 0 is Running, not Halted", exception.Message);
        }

        [Fact]
        public async Task OnErrorWithoutBody_ThrowsWithTheStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ResumeStreamPartition(connection, "q", "c", 0, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        }
    }

    public class ListAllDeadLetters
    {
        private static readonly DateTimeOffset DeadLetteredAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        private static DeadLetterDto[] Page(int start, int count)
            => Enumerable.Range(start, count)
                .Select(i => new DeadLetterDto($"m{i}", "q", null, "{}", "{}", DeadLetteredAt, null, null, "Pending", null, null))
                .ToArray();

        [Fact]
        public async Task WithAShortFirstPage_RequestsOnePageOfOneThousand()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Page(0, 3)) });
            var connection = ApiKeyConnection.Create(handler);

            var result = await FlowlyApiClient.ListAllDeadLetters(connection, "orders", null, "Pending", null, null, CancellationToken.None);

            Assert.Equal(3, result.Count);
            Assert.Equal(1, handler.RequestCount);
            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("take=1000", query);
            Assert.Contains("skip=0", query);
            Assert.Contains("queue=orders", query);
            Assert.Contains("status=Pending", query);
        }

        [Fact]
        public async Task WithFullPages_KeepsRequestingUntilAShortPageAdvancingSkip()
        {
            var skips = new List<string>();
            var handler = new RecordingHandler(request =>
            {
                var skip = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["skip"]!);
                skips.Add(skip.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(skip < 2005 ? Page(skip, 1000) : Page(skip, 7)) };
            });
            var connection = ApiKeyConnection.Create(handler);

            var result = await FlowlyApiClient.ListAllDeadLetters(connection, null, null, null, null, 5, CancellationToken.None);

            Assert.Equal(["5", "1005", "2005"], skips);
            Assert.Equal(2007, result.Count);
        }

        [Fact]
        public async Task WithAFullLastPage_RequestsOneMoreAndStopsOnTheEmptyPage()
        {
            var handler = new RecordingHandler(request =>
            {
                var skip = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["skip"]!);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(skip == 0 ? Page(0, 1000) : Array.Empty<DeadLetterDto>()) };
            });
            var connection = ApiKeyConnection.Create(handler);

            var result = await FlowlyApiClient.ListAllDeadLetters(connection, null, null, null, null, null, CancellationToken.None);

            Assert.Equal(1000, result.Count);
            Assert.Equal(2, handler.RequestCount);
        }
    }

    public class ListDeadLetters
    {
        private static readonly DateTimeOffset DeadLetteredAt = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

        [Fact]
        public async Task WithNoFilters_RequestsUnfilteredPath()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListDeadLetters(connection, null, null, null, null, null, null, CancellationToken.None);

            Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/dead-letters", handler.LastRequest.RequestUri!.AbsolutePath);
            Assert.Equal(string.Empty, handler.LastRequest.RequestUri.Query);
        }

        [Fact]
        public async Task WithFilters_IncludesThemEscapedInTheQueryString()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListDeadLetters(connection, "my-queue", "billing", "Pending", "id/with space", 20, 10, CancellationToken.None);

            var query = handler.LastRequest!.RequestUri!.Query;
            Assert.Contains("queue=my-queue", query);
            Assert.Contains("subscription=billing", query);
            Assert.Contains("status=Pending", query);
            Assert.Contains("messageId=id%2Fwith%20space", query);
            Assert.Contains("skip=20", query);
            Assert.Contains("take=10", query);
        }

        [Fact]
        public async Task DeserializesResponseBody()
        {
            var expected = new[] { new DeadLetterDto("m1", "q", null, "{}", "{}", DeadLetteredAt, "reason", null, "Pending", null, null) };
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });
            var connection = ApiKeyConnection.Create(handler);

            var result = await FlowlyApiClient.ListDeadLetters(connection, null, null, null, null, null, null, CancellationToken.None);

            Assert.Equal(expected[0], Assert.Single(result));
        }

        [Fact]
        public async Task DeserializesTheReplacedBody()
        {
            var json = "[{\"messageId\":\"m1\",\"queueName\":\"q\",\"messageBody\":\"{}\",\"messageProperties\":\"{}\",\"deadLetteredAtUtc\":\"2026-11-01T10:30:00+00:00\",\"status\":\"Requeued\",\"bodyReplaced\":true,\"replacementMessageBody\":\"{\\\"a\\\":1}\"}]";
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
            var connection = ApiKeyConnection.Create(handler);

            var deadLetter = Assert.Single(await FlowlyApiClient.ListDeadLetters(connection, null, null, null, null, null, null, CancellationToken.None));

            Assert.True(deadLetter.BodyReplaced);
            Assert.Equal("{\"a\":1}", deadLetter.ReplacementMessageBody);
            Assert.Equal("{}", deadLetter.MessageBody);
        }

        [Fact]
        public async Task DeserializesAResponseWithoutTheReplacementFieldsAsNotReplaced()
        {
            var json = "[{\"messageId\":\"m1\",\"queueName\":\"q\",\"messageBody\":\"{}\",\"messageProperties\":\"{}\",\"deadLetteredAtUtc\":\"2026-11-01T10:30:00+00:00\",\"status\":\"Pending\"}]";
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
            var connection = ApiKeyConnection.Create(handler);

            var deadLetter = Assert.Single(await FlowlyApiClient.ListDeadLetters(connection, null, null, null, null, null, null, CancellationToken.None));

            Assert.False(deadLetter.BodyReplaced);
            Assert.Null(deadLetter.ReplacementMessageBody);
        }

        [Fact]
        public async Task IncludesApiKeyHeader()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<DeadLetterDto>()) });
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.ListDeadLetters(connection, null, null, null, null, null, null, CancellationToken.None);

            Assert.Equal("the-api-key", handler.LastRequest!.Headers.GetValues("X-Flowly-Api-Key").Single());
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":\"'x' is not a dead letter status.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.ListDeadLetters(connection, null, null, "x", null, null, null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Contains("is not a dead letter status", exception.Message);
        }
    }

    public class RequeueDeadLetter
    {
        [Fact]
        public async Task PostsToRequeuePathWithJsonBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.RequeueDeadLetter(connection, "m1", "ops", CancellationToken.None);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/dead-letters/requeue", handler.LastRequest.RequestUri!.AbsolutePath);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal("m1", body.RootElement.GetProperty("messageId").GetString());
            Assert.Equal("ops", body.RootElement.GetProperty("requeuedBy").GetString());
        }

        [Fact]
        public async Task WithoutARequester_SendsANullRequeuedBy()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.RequeueDeadLetter(connection, "m1", null, CancellationToken.None);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("requeuedBy").ValueKind);
        }

        [Fact]
        public async Task WithAMessageBody_SendsItAsMessageBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.RequeueDeadLetter(connection, "m1", "ops", CancellationToken.None, "{\"amount\":12}");

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal("{\"amount\":12}", body.RootElement.GetProperty("messageBody").GetString());
        }

        [Fact]
        public async Task WithoutAMessageBody_SendsANullMessageBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.RequeueDeadLetter(connection, "m1", "ops", CancellationToken.None);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("messageBody").ValueKind);
        }

        [Fact]
        public async Task OnABadRequest_ThrowsWithTheValidationDetail()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"detail\":\"The replacement message body must not be empty.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.RequeueDeadLetter(connection, "m1", null, CancellationToken.None, ""));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Contains("must not be empty", exception.Message);
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"detail\":\"Dead letter 'm1' has status 'Requeued' and cannot be requeued.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.RequeueDeadLetter(connection, "m1", null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
            Assert.Contains("cannot be requeued", exception.Message);
        }

        [Fact]
        public async Task OnErrorWithoutBody_ThrowsWithTheStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.RequeueDeadLetter(connection, "m1", null, CancellationToken.None));

            Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        }
    }

    public class DiscardDeadLetter
    {
        [Fact]
        public async Task PostsToDiscardPathWithJsonBody()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
            var connection = ApiKeyConnection.Create(handler);

            await FlowlyApiClient.DiscardDeadLetter(connection, "m1", CancellationToken.None);

            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.Equal("/flowly/management/dead-letters/discard", handler.LastRequest.RequestUri!.AbsolutePath);

            var body = await JsonDocument.ParseAsync(await handler.LastRequestContent!.ReadAsStreamAsync());
            Assert.Equal("m1", body.RootElement.GetProperty("messageId").GetString());
        }

        [Fact]
        public async Task OnProblemResponse_ThrowsWithTheProblemDetailAndStatusCode()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"detail\":\"Dead letter with ID 'm1' was not found.\"}", System.Text.Encoding.UTF8, "application/problem+json")
            });
            var connection = ApiKeyConnection.Create(handler);

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                FlowlyApiClient.DiscardDeadLetter(connection, "m1", CancellationToken.None));

            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
            Assert.Contains("was not found", exception.Message);
        }
    }
}
