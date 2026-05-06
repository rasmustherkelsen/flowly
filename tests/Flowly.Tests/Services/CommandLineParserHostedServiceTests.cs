using System.Text.Json;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Services;
using Microsoft.Extensions.Hosting;

namespace Flowly.Tests.Services;

public class CommandLineParserHostedServiceTests
{
    public class StartingAsync
    {
        [Fact]
        public async Task WithoutOutputEnvVar_DoesNotWriteFile()
        {
            using var environmentSwitch = TemporaryEnvironmentVariable.Unset();
            var lifetime = new RecordingHostApplicationLifetime();
            var manifests = new[] { ManifestWithQueue("orders") };
            var commandLineParserHostedService = new CommandLineParserHostedService(manifests, lifetime);

            await commandLineParserHostedService.StartingAsync(CancellationToken.None);

            Assert.False(lifetime.StopApplicationCalled);
        }

        [Fact]
        public async Task WithOutputEnvVar_StopsApplication()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"flowly-discovery-{Guid.NewGuid():N}.json");
            using var environmentSwitch = TemporaryEnvironmentVariable.Set(outputPath);
            var lifetime = new RecordingHostApplicationLifetime();
            var manifests = new[] { ManifestWithQueue("orders") };
            var commandLineParserHostedService = new CommandLineParserHostedService(manifests, lifetime);

            try
            {
                await commandLineParserHostedService.StartingAsync(CancellationToken.None);

                Assert.True(lifetime.StopApplicationCalled);
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task WithOutputEnvVar_WritesQueueDiscoveryEntries()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"flowly-discovery-{Guid.NewGuid():N}.json");
            using var environmentSwitch = TemporaryEnvironmentVariable.Set(outputPath);
            var lifetime = new RecordingHostApplicationLifetime();
            var manifests = new[] { ManifestWithQueue("orders", requiresSession: true) };
            var commandLineParserHostedService = new CommandLineParserHostedService(manifests, lifetime);

            try
            {
                await commandLineParserHostedService.StartingAsync(CancellationToken.None);

                var json = await File.ReadAllTextAsync(outputPath);
                using var document = JsonDocument.Parse(json);
                var queues = document.RootElement.GetProperty("Queues");

                Assert.Equal(1, queues.GetArrayLength());
                Assert.Equal("orders", queues[0].GetProperty("QueueName").GetString());
                Assert.Equal("test-provider", queues[0].GetProperty("ProviderName").GetString());
                Assert.True(queues[0].GetProperty("IsPrimary").GetBoolean());
                Assert.True(queues[0].GetProperty("RequiresSession").GetBoolean());
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task WithOutputEnvVar_WritesEventDiscoveryEntries()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"flowly-discovery-{Guid.NewGuid():N}.json");
            using var environmentSwitch = TemporaryEnvironmentVariable.Set(outputPath);
            var lifetime = new RecordingHostApplicationLifetime();
            var manifest = new ProviderQueueManifest("test-provider", true, "Stub");
            manifest.AddEvent(new DeferredEventRegistration("order-placed", "notify-subscriber"));
            var commandLineParserHostedService = new CommandLineParserHostedService([manifest], lifetime);

            try
            {
                await commandLineParserHostedService.StartingAsync(CancellationToken.None);

                var json = await File.ReadAllTextAsync(outputPath);
                using var document = JsonDocument.Parse(json);
                var events = document.RootElement.GetProperty("Events");

                Assert.Equal(1, events.GetArrayLength());
                Assert.Equal("order-placed", events[0].GetProperty("TopicName").GetString());
                Assert.Equal("notify-subscriber", events[0].GetProperty("SubscriptionName").GetString());
                Assert.Equal("test-provider", events[0].GetProperty("ProviderName").GetString());
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task WithOutputEnvVar_FormatsTimeSpansWithCSpecifier()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"flowly-discovery-{Guid.NewGuid():N}.json");
            using var environmentSwitch = TemporaryEnvironmentVariable.Set(outputPath);
            var lifetime = new RecordingHostApplicationLifetime();
            var manifest = new ProviderQueueManifest("test-provider", true, "Stub");
            manifest.Add(new DeferredQueueRegistration(
                "orders",
                DefaultMessageTimeToLive: TimeSpan.FromMinutes(15),
                LockDuration: TimeSpan.FromMinutes(2)));
            var commandLineParserHostedService = new CommandLineParserHostedService([manifest], lifetime);

            try
            {
                await commandLineParserHostedService.StartingAsync(CancellationToken.None);

                var json = await File.ReadAllTextAsync(outputPath);
                using var document = JsonDocument.Parse(json);
                var queue = document.RootElement.GetProperty("Queues")[0];

                Assert.Equal(TimeSpan.FromMinutes(15).ToString("c"), queue.GetProperty("DefaultMessageTimeToLive").GetString());
                Assert.Equal(TimeSpan.FromMinutes(2).ToString("c"), queue.GetProperty("LockDuration").GetString());
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        [Fact]
        public async Task WithMultipleManifests_WritesAllQueueEntries()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"flowly-discovery-{Guid.NewGuid():N}.json");
            using var environmentSwitch = TemporaryEnvironmentVariable.Set(outputPath);
            var lifetime = new RecordingHostApplicationLifetime();
            var manifests = new[]
            {
                ManifestWithQueue("orders", providerName: "p1", isPrimary: true),
                ManifestWithQueue("payments", providerName: "p2", isPrimary: false)
            };
            var commandLineParserHostedService = new CommandLineParserHostedService(manifests, lifetime);

            try
            {
                await commandLineParserHostedService.StartingAsync(CancellationToken.None);

                var json = await File.ReadAllTextAsync(outputPath);
                using var document = JsonDocument.Parse(json);
                var queues = document.RootElement.GetProperty("Queues");

                Assert.Equal(2, queues.GetArrayLength());
            }
            finally
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }
    }

    public class StartAsync
    {
        [Fact]
        public async Task ReturnsCompletedTask()
        {
            var commandLineParserHostedService = new CommandLineParserHostedService([], new RecordingHostApplicationLifetime());

            var task = commandLineParserHostedService.StartAsync(CancellationToken.None);

            Assert.True(task.IsCompletedSuccessfully);
            await task;
        }
    }

    public class StoppingAsync
    {
        [Fact]
        public async Task ReturnsCompletedTask()
        {
            var commandLineParserHostedService = new CommandLineParserHostedService([], new RecordingHostApplicationLifetime());

            var task = commandLineParserHostedService.StoppingAsync(CancellationToken.None);

            Assert.True(task.IsCompletedSuccessfully);
            await task;
        }
    }

    private static ProviderQueueManifest ManifestWithQueue(
        string queueName,
        bool requiresSession = false,
        string providerName = "test-provider",
        bool isPrimary = true)
    {
        var manifest = new ProviderQueueManifest(providerName, isPrimary, "Stub");
        manifest.Add(new DeferredQueueRegistration(queueName, requiresSession));
        return manifest;
    }

    private sealed class TemporaryEnvironmentVariable : IDisposable
    {
        private readonly string? _previousValue;

        private TemporaryEnvironmentVariable(string? newValue)
        {
            _previousValue = Environment.GetEnvironmentVariable(CommandLineParserHostedServiceDefinitions.OutputFileEnvVar);
            Environment.SetEnvironmentVariable(CommandLineParserHostedServiceDefinitions.OutputFileEnvVar, newValue);
        }

        public static TemporaryEnvironmentVariable Set(string value) => new(value);

        public static TemporaryEnvironmentVariable Unset() => new(null);

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(CommandLineParserHostedServiceDefinitions.OutputFileEnvVar, _previousValue);
        }
    }

    private sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopApplicationCalled { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            StopApplicationCalled = true;
        }
    }
}
