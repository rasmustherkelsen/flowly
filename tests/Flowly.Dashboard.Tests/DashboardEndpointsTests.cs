using System.Net;
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
        public async Task WithoutParameters_QueriesEverythingWithoutAnExtraCount()
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
