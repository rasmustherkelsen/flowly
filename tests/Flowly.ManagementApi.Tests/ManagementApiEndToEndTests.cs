using System.Net;
using System.Net.Http.Json;
using Flowly.MessageStreams;
using Flowly.MessageStreams.DatabaseModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flowly.ManagementApi.Tests;

public class ManagementApiEndToEndTests
{
    private const string ChecklistPath = "/flowly/management/message-streams/checkpoints";
    private const string ResetPath = "/flowly/management/message-streams/checkpoints/reset";

    public class ApiKeyAuthorization
    {
        [Fact]
        public async Task WithoutApiKeyHeader_Returns401()
        {
            using var fixture = new HostFixture();

            var response = await fixture.Client.GetAsync(ChecklistPath);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithWrongApiKey_Returns401()
        {
            using var fixture = new HostFixture();
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", "not-a-real-key");

            var response = await fixture.Client.GetAsync(ChecklistPath);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CanList()
        {
            using var fixture = new HostFixture();
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await fixture.Client.GetAsync(ChecklistPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_CannotReset()
        {
            using var fixture = new HostFixture();
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await fixture.Client.PostAsJsonAsync(ResetPath, new { QueueName = "q", ConsumerName = "c", Offset = 1 });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task WithWriteKey_CanResetAndThenListShowsNewPosition()
        {
            using var fixture = new HostFixture();
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.WriteKey);

            var resetResponse = await fixture.Client.PostAsJsonAsync(ResetPath, new { QueueName = "my-queue", ConsumerName = "MyHandler", Offset = 99 });
            Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

            var listResponse = await fixture.Client.GetAsync($"{ChecklistPath}?queue=my-queue&consumer=MyHandler");
            var body = await listResponse.Content.ReadAsStringAsync();
            Assert.Contains("99", body);
        }
    }

    public class Discovery
    {
        [Fact]
        public async Task WithoutApiKey_DiscoveryDocumentIsProtected()
        {
            using var fixture = new HostFixture();

            var response = await fixture.Client.GetAsync("/flowly/management/openapi.json");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithReadKey_DiscoveryDocumentIsAccessible()
        {
            using var fixture = new HostFixture();
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await fixture.Client.GetAsync("/flowly/management/openapi.json");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    public class FeatureDetection
    {
        [Fact]
        public async Task WithoutCheckpointPersistenceRegistered_RoutesDoNotExist()
        {
            using var fixture = new HostFixture(registerPersistence: false);
            fixture.Client.DefaultRequestHeaders.Add("X-Flowly-Api-Key", HostFixture.ReadKey);

            var response = await fixture.Client.GetAsync(ChecklistPath);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class Configuration
    {
        [Fact]
        public void WithoutAuthorizationConfigured_ThrowsAtUseTime()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddAuthorization();
                        services.AddFlowlyManagementApi(_ => { });
                    });
                    webHost.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.UseFlowlyManagementApi());
                    });
                });

            Assert.Throws<InvalidOperationException>(() => host.Start());
        }
    }

    private sealed class HostFixture : IDisposable
    {
        public const string ReadKey = "test-read-key";
        public const string WriteKey = "test-write-key";

        private readonly IHost _host;
        private readonly SqliteConnection? _keepAliveConnection;

        public HttpClient Client { get; }

        public HostFixture(bool registerPersistence = true)
        {
            SqliteConnection? keepAlive = null;

            _host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddFlowlyManagementApi(options => options.RequireApiKey(ReadKey, WriteKey));

                        if (registerPersistence)
                        {
                            var connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
                            keepAlive = new SqliteConnection(connectionString);
                            keepAlive.Open();

                            var builder = new DbContextOptionsBuilder<MessageStreamCheckpointDataContext>();
                            builder.UseSqlite(connectionString);
                            var factory = new PooledDbContextFactory<MessageStreamCheckpointDataContext>(builder.Options);

                            using (var context = factory.CreateDbContext())
                                context.Database.EnsureCreated();

                            var registry = new MessageStreamCheckpointStoreRegistry();
                            registry.Register(null, factory);
                            services.AddSingleton(registry);
                        }
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

            _keepAliveConnection = keepAlive;
            Client = _host.GetTestServer().CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
            _keepAliveConnection?.Dispose();
        }
    }
}
