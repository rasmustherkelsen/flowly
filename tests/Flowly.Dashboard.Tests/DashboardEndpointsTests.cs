using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.Dashboard.Tests.Fakes;
using Flowly.DeadLetters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.Dashboard.Tests;

public class DashboardEndpointsTests
{
    public class GetDeadLetters
    {
        [Fact]
        public async Task WithoutDeadLetterTracking_Returns404()
        {
            using var client = CreateClient(null);

            var response = await client.GetAsync("/flowly/api/dead-letters");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task WithoutParameters_QueriesTheDefaultPageWithoutAnExtraCount()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync("/flowly/api/dead-letters");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(new DeadLetterQuery(), Assert.Single(deadLetterService.Queries));
            Assert.False(response.Headers.Contains(DashboardEndpoints.TotalCountHeader));
        }

        [Fact]
        public async Task WithStatusSkipAndTake_PassesThemToTheQueryAndRequestsTheTotal()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            await client.GetAsync("/flowly/api/dead-letters?status=requeued&skip=40&take=20");

            var deadLetterQuery = Assert.Single(deadLetterService.Queries);
            Assert.Equal(DeadLetterStatus.Requeued, deadLetterQuery.Status);
            Assert.Equal(40, deadLetterQuery.Skip);
            Assert.Equal(20, deadLetterQuery.Take);
            Assert.True(deadLetterQuery.IncludeTotalCount);
        }

        [Fact]
        public async Task WithEmptyStatus_DoesNotFilterOnStatus()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            await client.GetAsync("/flowly/api/dead-letters?status=&take=20");

            Assert.Null(Assert.Single(deadLetterService.Queries).Status);
        }

        [Fact]
        public async Task ReturnsTheTotalCountInAHeader()
        {
            var deadLetterService = new RecordingDeadLetterService { PageToReturn = new DeadLetterPage([new FakeDeadLetter { MessageId = "m1" }], 57) };
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync("/flowly/api/dead-letters?take=1");

            Assert.Equal("57", Assert.Single(response.Headers.GetValues(DashboardEndpoints.TotalCountHeader)));
        }

        [Fact]
        public async Task ReturnsTheItemsAsAJsonArrayWithTheSameFieldsAsBefore()
        {
            var deadLetterService = new RecordingDeadLetterService
            {
                PageToReturn = new DeadLetterPage([new FakeDeadLetter { MessageId = "m1", QueueName = "orders", DeadLetterReason = "boom", Status = DeadLetterStatus.Requeued, RequeuedBy = "ops" }], null)
            };
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync("/flowly/api/dead-letters");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal("m1", deadLetter.GetProperty("messageId").GetString());
            Assert.Equal("orders", deadLetter.GetProperty("queueName").GetString());
            Assert.Equal("boom", deadLetter.GetProperty("deadLetterReason").GetString());
            Assert.Equal("Requeued", deadLetter.GetProperty("status").GetString());
            Assert.Equal("ops", deadLetter.GetProperty("requeuedBy").GetString());
            Assert.Equal("{}", deadLetter.GetProperty("messageBody").GetString());
            Assert.Equal("{}", deadLetter.GetProperty("messageProperties").GetString());
            Assert.True(deadLetter.TryGetProperty("deadLetteredAt", out _));
        }

        [Fact]
        public async Task DoesNotLoadEveryDeadLetter()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            await client.GetAsync("/flowly/api/dead-letters?take=20");

            Assert.Equal(0, deadLetterService.GetDeadLettersCalls);
        }

        [Theory]
        [InlineData("status=Discarded")]
        [InlineData("skip=-1")]
        [InlineData("take=0")]
        [InlineData("take=1001")]
        public async Task WithAnInvalidParameter_Returns400WithoutQuerying(string queryString)
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync($"/flowly/api/dead-letters?{queryString}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(deadLetterService.Queries);
        }
    }

    public class RequeueDeadLetter
    {
        [Fact]
        public async Task WithoutDeadLetterTracking_Returns404()
        {
            using var client = CreateClient(null);

            var response = await client.PostAsync("/flowly/api/dead-letters/m1/requeue", null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task WithoutARequestBody_RequeuesTheStoredBody()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsync("/flowly/api/dead-letters/m1/requeue", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(("m1", null, null), Assert.Single(deadLetterService.RequeueCalls));
        }

        [Fact]
        public async Task WithAnEmptyJsonObject_RequeuesTheStoredBody()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsJsonAsync("/flowly/api/dead-letters/m1/requeue", new { });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(("m1", null, null), Assert.Single(deadLetterService.RequeueCalls));
        }

        [Fact]
        public async Task WithAMessageBody_PassesItToTheService()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsJsonAsync("/flowly/api/dead-letters/m1/requeue", new { messageBody = """{"amount":12}""" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(("m1", null, """{"amount":12}"""), Assert.Single(deadLetterService.RequeueCalls));
        }

        [Fact]
        public async Task WithAMalformedJsonBody_Returns400WithoutRequeuing()
        {
            var deadLetterService = new RecordingDeadLetterService();
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsync("/flowly/api/dead-letters/m1/requeue", new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(deadLetterService.RequeueCalls);
        }

        [Fact]
        public async Task WhenTheReplacementIsRejected_Returns400WithTheReason()
        {
            var deadLetterService = new RecordingDeadLetterService { RequeueFailure = new InvalidReplacementMessageBodyException("The replacement message body must not be empty.") };
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsJsonAsync("/flowly/api/dead-letters/m1/requeue", new { messageBody = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("must not be empty", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ForAnUnknownMessageId_Returns404()
        {
            var deadLetterService = new RecordingDeadLetterService { RequeueFailure = new KeyNotFoundException() };
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsJsonAsync("/flowly/api/dead-letters/m1/requeue", new { messageBody = "{}" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ForAMessageThatIsNotPending_Returns400()
        {
            var deadLetterService = new RecordingDeadLetterService { RequeueFailure = new InvalidOperationException("cannot be requeued") };
            using var client = CreateClient(deadLetterService);

            var response = await client.PostAsJsonAsync("/flowly/api/dead-letters/m1/requeue", new { messageBody = "{}" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("cannot be requeued", await response.Content.ReadAsStringAsync());
        }
    }

    public class GetDeadLettersReplacementBody
    {
        [Fact]
        public async Task ReturnsTheReplacementMessageBodyNextToTheOriginal()
        {
            var deadLetterService = new RecordingDeadLetterService
            {
                PageToReturn = new DeadLetterPage([new FakeDeadLetter { MessageId = "m1", MessageBody = "original", Status = DeadLetterStatus.Requeued, ReplacementMessageBody = "corrected" }], null)
            };
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync("/flowly/api/dead-letters");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var deadLetter = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal("original", deadLetter.GetProperty("messageBody").GetString());
            Assert.Equal("corrected", deadLetter.GetProperty("replacementMessageBody").GetString());
        }

        [Fact]
        public async Task ReturnsANullReplacementMessageBodyWhenTheBodyWasNotReplaced()
        {
            var deadLetterService = new RecordingDeadLetterService { PageToReturn = new DeadLetterPage([new FakeDeadLetter { MessageId = "m1" }], null) };
            using var client = CreateClient(deadLetterService);

            var response = await client.GetAsync("/flowly/api/dead-letters");

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null, Assert.Single(json.RootElement.EnumerateArray()).GetProperty("replacementMessageBody").ValueKind);
        }
    }

    private static HttpClient CreateClient(RecordingDeadLetterService? deadLetterService)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    services.AddFlowlyDashboard();

                    if (deadLetterService is not null)
                    {
                        services.AddSingleton<IDeadLetterService>(deadLetterService);
                    }
                });
                webHost.Configure(app => app.UseFlowlyDashboard());
            })
            .Start();

        return host.GetTestServer().CreateClient();
    }
}
