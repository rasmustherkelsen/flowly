using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class CrossProviderConflictValidatorTests
{
    public class Validate
    {
        [Fact]
        public void SingleProvider_DoesNotThrow()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", ("order-placed", null, null, null))
            };

            validator.Validate(manifests);
        }

        [Fact]
        public void MultipleProviders_DisjointQueueNames_DoesNotThrow()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", ("order-placed", null, null, null)),
                ManifestWith("rabbit", "RabbitMQ", ("invoice-created", null, null, null))
            };

            validator.Validate(manifests);
        }

        [Fact]
        public void SameTransportType_SameQueueName_IdenticalSettings_DoesNotThrow()
        {
            var ttl = TimeSpan.FromDays(2);
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb-primary", "AzureServiceBus", ("order-placed", ttl, null, null)),
                ManifestWith("asb-secondary", "AzureServiceBus", ("order-placed", ttl, null, null))
            };

            validator.Validate(manifests);
        }

        [Fact]
        public void SameTransportType_SameQueueName_NullVsConcreteSettings_DoesNotThrow()
        {
            var ttl = TimeSpan.FromDays(2);
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb-primary", "AzureServiceBus", ("order-placed", null, null, null)),
                ManifestWith("asb-secondary", "AzureServiceBus", ("order-placed", ttl, null, null))
            };

            validator.Validate(manifests);
        }

        [Fact]
        public void SameTransportType_SameQueueName_ConflictingTtl_Throws()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb-primary", "AzureServiceBus", ("order-placed", TimeSpan.FromDays(1), null, null)),
                ManifestWith("asb-secondary", "AzureServiceBus", ("order-placed", TimeSpan.FromDays(7), null, null))
            };

            var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(manifests));

            Assert.Contains("order-placed", exception.Message);
            Assert.Contains("DefaultMessageTimeToLive", exception.Message);
            Assert.Contains("asb-primary", exception.Message);
            Assert.Contains("asb-secondary", exception.Message);
        }

        [Fact]
        public void SameTransportType_SameQueueName_ConflictingLockDuration_Throws()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb-primary", "AzureServiceBus", ("order-placed", null, null, TimeSpan.FromMinutes(5))),
                ManifestWith("asb-secondary", "AzureServiceBus", ("order-placed", null, null, TimeSpan.FromMinutes(10)))
            };

            Assert.Throws<InvalidOperationException>(() => validator.Validate(manifests));
        }

        [Fact]
        public void SameTransportType_SameQueueName_ConflictingDeadLetterSetting_Throws()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb-primary", "AzureServiceBus", ("order-placed", null, true, null)),
                ManifestWith("asb-secondary", "AzureServiceBus", ("order-placed", null, false, null))
            };

            Assert.Throws<InvalidOperationException>(() => validator.Validate(manifests));
        }

        [Fact]
        public void DifferentTransportTypes_SameQueueName_DoesNotThrow()
        {
            var validator = Validator();
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", ("order-placed", null, null, null)),
                ManifestWith("rabbit", "RabbitMQ", ("order-placed", null, null, null))
            };

            validator.Validate(manifests);
        }

        [Fact]
        public void DifferentTransportTypes_SameQueueName_LogsWarning()
        {
            var capturingLogger = new CapturingLogger();
            var validator = new CrossProviderConflictValidator(capturingLogger);
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", ("order-placed", null, null, null)),
                ManifestWith("rabbit", "RabbitMQ", ("order-placed", null, null, null))
            };

            validator.Validate(manifests);

            Assert.Single(capturingLogger.Warnings);
            Assert.Contains("order-placed", capturingLogger.Warnings[0]);
            Assert.Contains("AzureServiceBus", capturingLogger.Warnings[0]);
            Assert.Contains("RabbitMQ", capturingLogger.Warnings[0]);
        }

        [Fact]
        public void DifferentTransportTypes_MultipleSharedQueueNames_LogsOneWarningPerQueue()
        {
            var capturingLogger = new CapturingLogger();
            var validator = new CrossProviderConflictValidator(capturingLogger);
            var manifests = new[]
            {
                ManifestWith("asb", "AzureServiceBus", ("order-placed", null, null, null), ("invoice-created", null, null, null)),
                ManifestWith("rabbit", "RabbitMQ", ("order-placed", null, null, null), ("invoice-created", null, null, null))
            };

            validator.Validate(manifests);

            Assert.Equal(2, capturingLogger.Warnings.Count);
        }

        private static CrossProviderConflictValidator Validator() =>
            new(new NullLogger<CrossProviderConflictValidator>());

        private static ProviderQueueManifest ManifestWith(
            string providerName,
            string transportType,
            params (string QueueName, TimeSpan? Ttl, bool? DeadLetter, TimeSpan? LockDuration)[] queues)
        {
            var manifest = new ProviderQueueManifest(providerName, isPrimary: false, transportType);

            foreach (var (queueName, ttl, deadLetter, lockDuration) in queues)
            {
                manifest.Add(new DeferredQueueRegistration(
                    queueName,
                    DefaultMessageTimeToLive: ttl,
                    DeadLetterOnMessageExpiration: deadLetter,
                    LockDuration: lockDuration));
            }

            return manifest;
        }

        private sealed class CapturingLogger : ILogger<CrossProviderConflictValidator>
        {
            public List<string> Warnings { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                    Warnings.Add(formatter(state, exception));
            }
        }
    }
}