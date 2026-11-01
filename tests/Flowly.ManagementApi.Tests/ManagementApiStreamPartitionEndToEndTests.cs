using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flowly.ManagementApi.Tests.Fakes;
using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.ManagementApi.Tests;

public class ManagementApiStreamPartitionEndToEndTests
{
    private const string ListPath = "/flowly/management/message-streams/partitions";
    private const string ResumePath = "/flowly/management/message-streams/partitions/resume";

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
        public async Task ResumeWithoutApiKeyHeader_Returns401()
        {
            using var hostFixture = new HostFixture();

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CanList()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CannotResume()
        {
            using var hostFixture = new HostFixture();
            var partition = hostFixture.Partitions.Add("orders", "OrderConsumer", 0, StreamPartitionState.Halted);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(0, partition.ResumeCalls);
        }

        [Fact]
        public async Task WithWriteKey_CanResume()
        {
            using var hostFixture = new HostFixture();
            var partition = hostFixture.Partitions.Add("orders", "OrderConsumer", 0, StreamPartitionState.Halted);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(1, partition.ResumeCalls);
        }
    }

    public class ListPartitions
    {
        [Fact]
        public async Task ReturnsTheStateAndTheLastErrorAsJson()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Partitions.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var partition = Assert.Single(body.RootElement.EnumerateArray());
            Assert.Equal("orders", partition.GetProperty("queueName").GetString());
            Assert.Equal("OrderConsumer", partition.GetProperty("consumerName").GetString());
            Assert.Equal(1, partition.GetProperty("partition").GetInt32());
            Assert.Equal("Halted", partition.GetProperty("state").GetString());
            Assert.Equal("poison", partition.GetProperty("lastError").GetProperty("message").GetString());
        }

        [Fact]
        public async Task AppliesTheStateFilterFromTheQueryString()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Partitions.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            hostFixture.Partitions.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync($"{ListPath}?state=halted&queue=orders&consumer=OrderConsumer&partition=1");

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, Assert.Single(body.RootElement.EnumerateArray()).GetProperty("partition").GetInt32());
        }

        [Fact]
        public async Task WithAnUnknownState_Returns400()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync($"{ListPath}?state=stuck");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    public class ResumePartition
    {
        [Fact]
        public async Task ThenListShowsThePartitionRunningAgainWithItsLastError()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Partitions.Add("orders", "OrderConsumer", 0, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });
            var response = await hostFixture.Client.GetAsync(ListPath);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var partition = Assert.Single(body.RootElement.EnumerateArray());
            Assert.Equal("Running", partition.GetProperty("state").GetString());
            Assert.Equal("poison", partition.GetProperty("lastError").GetProperty("message").GetString());
        }

        [Fact]
        public async Task ForAnUnknownPartition_Returns404WithAProblemBody()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 3 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("partition 3", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ForARunningPartition_Returns409()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Partitions.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task ForANonPartitionedStream_AcceptsARequestWithoutPartition()
        {
            using var hostFixture = new HostFixture();
            var partition = hostFixture.Partitions.Add("orders", "OrderConsumer", null, StreamPartitionState.Halted);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer" });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(1, partition.ResumeCalls);
        }
    }

    public class FeatureDetection
    {
        [Fact]
        public async Task WithoutStreamPartitionServicesRegistered_ListRouteDoesNotExist()
        {
            using var hostFixture = new HostFixture(registerStreamPartitionServices: false);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync(ListPath);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task WithoutStreamPartitionServicesRegistered_ResumeRouteDoesNotExist()
        {
            using var hostFixture = new HostFixture(registerStreamPartitionServices: false);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var response = await hostFixture.Client.PostAsJsonAsync(ResumePath, new { QueueName = "orders", ConsumerName = "OrderConsumer", Partition = 0 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class Discovery
    {
        [Fact]
        public async Task DiscoveryDocumentDescribesBothPartitionActions()
        {
            using var hostFixture = new HostFixture();
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync("/flowly/management/openapi.json");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            var list = paths.GetProperty(ListPath).GetProperty("get");
            var resume = paths.GetProperty(ResumePath).GetProperty("post");
            Assert.Equal("ListMessageStreamPartitions", list.GetProperty("operationId").GetString());
            Assert.Equal("ResumeMessageStreamPartition", resume.GetProperty("operationId").GetString());
            Assert.Equal("message-streams", list.GetProperty("tags")[0].GetString());
            Assert.Equal("message-streams", resume.GetProperty("tags")[0].GetString());
            Assert.True(resume.GetProperty("responses").TryGetProperty("409", out _));
        }

        [Fact]
        public async Task WithoutStreamPartitionServicesRegistered_DiscoveryDocumentOmitsThePartitionActions()
        {
            using var hostFixture = new HostFixture(registerStreamPartitionServices: false);
            hostFixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await hostFixture.Client.GetAsync("/flowly/management/openapi.json");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(document.RootElement.GetProperty("paths").TryGetProperty(ListPath, out _));
        }
    }

    private sealed class HostFixture : IDisposable
    {
        public const string ReadKey = "test-read-key";
        public const string WriteKey = "test-write-key";

        private readonly IHost _host;

        public HttpClient Client { get; }

        public FakeStreamPartitionHost Partitions { get; } = new(TimeProvider.System);

        public HostFixture(bool registerStreamPartitionServices = true)
        {
            _host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddFlowlyManagementApi(options => options.RequireApiKey(ReadKey, WriteKey));

                        if (registerStreamPartitionServices)
                            Partitions.AddTo(services);
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

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
        }
    }
}
