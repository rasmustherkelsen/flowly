using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.DeadLetters;
using Flowly.ManagementApi.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.ManagementApi.Tests;

public class ManagementApiDeadLetterEndToEndTests
{
    private const string ListPath = "/flowly/management/dead-letters";
    private const string RequeuePath = "/flowly/management/dead-letters/requeue";
    private const string DiscardPath = "/flowly/management/dead-letters/discard";

    public class ApiKeyAuthorization
    {
        [Fact]
        public async Task ListWithoutApiKeyHeader_Returns401()
        {
            using var hostFixture = new HostFixture();

            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RequeueWithoutApiKeyHeader_Returns401()
        {
            using var hostFixture = new HostFixture();

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CanList()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CannotRequeue()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(hostFixture.DeadLetters.RequeueCalls);
        }

        [Fact]
        public async Task WithReadKey_CannotDiscard()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(hostFixture.DeadLetters.DiscardCalls);
        }

        [Fact]
        public async Task WithWriteKey_CanRequeueAndDiscard()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.DeadLetters.Add("m2");
            hostFixture.UseKey(HostFixture.WriteKey);

            var requeued = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });
            var discarded = await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "m2" });

            Assert.Equal(HttpStatusCode.NoContent, requeued.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
        }
    }

    public class ListDeadLetters
    {
        [Fact]
        public async Task ReturnsTheDeadLettersAsCamelCaseJson()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1", "orders", status: DeadLetterStatus.Pending, reason: "MaxDeliveryCountExceeded");
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(body.RootElement.EnumerateArray());
            Assert.Equal("m1", deadLetter.GetProperty("messageId").GetString());
            Assert.Equal("orders", deadLetter.GetProperty("queueName").GetString());
            Assert.Equal("Pending", deadLetter.GetProperty("status").GetString());
            Assert.Equal("MaxDeliveryCountExceeded", deadLetter.GetProperty("deadLetterReason").GetString());
            Assert.Equal(JsonValueKind.Null, deadLetter.GetProperty("subscriptionName").ValueKind);
        }

        [Fact]
        public async Task AppliesTheFiltersAndPagingFromTheQueryString()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("a", "orders", deadLetteredAt: new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
            hostFixture.DeadLetters.Add("b", "orders", deadLetteredAt: new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero));
            hostFixture.DeadLetters.Add("c", "invoices");
            hostFixture.DeadLetters.Add("d", "orders", status: DeadLetterStatus.Requeued);
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync($"{ListPath}?queue=orders&status=pending&skip=1&take=1");

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("a", Assert.Single(body.RootElement.EnumerateArray()).GetProperty("messageId").GetString());
        }

        [Fact]
        public async Task WithAnUnknownStatus_Returns400WithAProblemBody()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync($"{ListPath}?status=Discarded");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Pending, Requeued", await response.Content.ReadAsStringAsync());
        }
    }

    public class RequeueDeadLetter
    {
        [Fact]
        public async Task StoresTheRequesterAndThenListShowsTheDeadLetterRequeued()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.WriteKey);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", RequeuedBy = "ops" });
            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(body.RootElement.EnumerateArray());
            Assert.Equal("Requeued", deadLetter.GetProperty("status").GetString());
            Assert.Equal("ops", deadLetter.GetProperty("requeuedBy").GetString());
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404WithAProblemBody()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "missing" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("missing", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1", status: DeadLetterStatus.Requeued);
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task WithABlankMessageId_Returns400()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = " " });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    public class RequeueDeadLetterWithAMessageBody
    {
        [Fact]
        public async Task ThenListShowsBodyReplacedTheReplacementAndTheOriginalBody()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.WriteKey);

            var requeued = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = """{"amount":12}""" });
            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.NoContent, requeued.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(body.RootElement.EnumerateArray());
            Assert.True(deadLetter.GetProperty("bodyReplaced").GetBoolean());
            Assert.Equal("""{"amount":12}""", deadLetter.GetProperty("replacementMessageBody").GetString());
            Assert.Equal("{}", deadLetter.GetProperty("messageBody").GetString());
        }

        [Fact]
        public async Task WithoutAMessageBody_ListShowsBodyNotReplaced()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.WriteKey);

            await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });
            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(body.RootElement.EnumerateArray());
            Assert.False(deadLetter.GetProperty("bodyReplaced").GetBoolean());
            Assert.Equal(JsonValueKind.Null, deadLetter.GetProperty("replacementMessageBody").ValueKind);
        }

        [Fact]
        public async Task WithABlankMessageBody_Returns400AndLeavesTheDeadLetterPending()
        {
            using var hostFixture = new HostFixture();
            var deadLetter = hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("must not be empty", await response.Content.ReadAsStringAsync());
            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "missing", MessageBody = "{}" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1", status: DeadLetterStatus.Requeued);
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "{}" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_IsForbidden()
        {
            using var hostFixture = new HostFixture();
            var deadLetter = hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1", MessageBody = "{}" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(DeadLetterStatus.Pending, deadLetter.Status);
        }
    }

    public class DiscardDeadLetter
    {
        [Fact]
        public async Task ThenListNoLongerShowsTheDeadLetter()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1");
            hostFixture.UseKey(HostFixture.WriteKey);

            await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "m1" });
            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Empty(body.RootElement.EnumerateArray());
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "missing" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ForAnAlreadyRequeuedDeadLetter_Returns409()
        {
            using var hostFixture = new HostFixture();
            hostFixture.DeadLetters.Add("m1", status: DeadLetterStatus.Requeued);
            hostFixture.UseKey(HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    public class FeatureDetection
    {
        [Fact]
        public async Task WithoutADeadLetterServiceRegistered_ListRouteDoesNotExist()
        {
            using var hostFixture = new HostFixture(registerDeadLetterService: false);
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task WithoutADeadLetterServiceRegistered_RequeueAndDiscardRoutesDoNotExist()
        {
            using var hostFixture = new HostFixture(registerDeadLetterService: false);
            hostFixture.UseKey(HostFixture.WriteKey);

            var requeued = await hostFixture.Client.PostAsJsonAsync(RequeuePath, new { MessageId = "m1" });
            var discarded = await hostFixture.Client.PostAsJsonAsync(DiscardPath, new { MessageId = "m1" });

            Assert.Equal(HttpStatusCode.NotFound, requeued.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, discarded.StatusCode);
        }
    }

    public class Discovery
    {
        [Fact]
        public async Task DiscoveryDocumentDescribesTheThreeDeadLetterActions()
        {
            using var hostFixture = new HostFixture();
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync("/flowly/management/openapi.json");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            var list = paths.GetProperty(ListPath).GetProperty("get");
            var requeue = paths.GetProperty(RequeuePath).GetProperty("post");
            var discard = paths.GetProperty(DiscardPath).GetProperty("post");
            Assert.Equal("ListDeadLetters", list.GetProperty("operationId").GetString());
            Assert.Equal("RequeueDeadLetter", requeue.GetProperty("operationId").GetString());
            Assert.Equal("DiscardDeadLetter", discard.GetProperty("operationId").GetString());
            Assert.Equal("dead-letters", list.GetProperty("tags")[0].GetString());
            Assert.Equal("dead-letters", requeue.GetProperty("tags")[0].GetString());
            Assert.Equal("dead-letters", discard.GetProperty("tags")[0].GetString());
            Assert.True(requeue.GetProperty("responses").TryGetProperty("409", out _));
            Assert.True(discard.GetProperty("responses").TryGetProperty("404", out _));
        }

        [Fact]
        public async Task WithoutADeadLetterServiceRegistered_DiscoveryDocumentOmitsTheDeadLetterActions()
        {
            using var hostFixture = new HostFixture(registerDeadLetterService: false);
            hostFixture.UseKey(HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync("/flowly/management/openapi.json");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            Assert.False(paths.TryGetProperty(ListPath, out _));
            Assert.False(paths.TryGetProperty(RequeuePath, out _));
            Assert.False(paths.TryGetProperty(DiscardPath, out _));
        }
    }

    private sealed class HostFixture : IDisposable
    {
        public const string ReadKey = "test-read-key";
        public const string WriteKey = "test-write-key";

        private readonly IHost _host;

        public HttpClient Client { get; }

        public FakeDeadLetterService DeadLetters { get; } = new();

        public HostFixture(bool registerDeadLetterService = true)
        {
            _host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.UseDefaultServiceProvider(options => options.ValidateScopes = true);
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddFlowlyManagementApi(options => options.RequireApiKey(ReadKey, WriteKey));

                        if (registerDeadLetterService)
                            DeadLetters.AddTo(services);
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.UseFlowlyManagementApi());
                    });
                })
                .Start();

            Client = _host.GetTestServer().CreateClient();
        }

        public void UseKey(string key) => Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", key);

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
        }
    }
}
