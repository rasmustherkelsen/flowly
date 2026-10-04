using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Repositories;
using Flowly.DeadLetters.Services;
using Flowly.DeadLetters.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.Tests.Services;

public class DeadLetterIngestionHealthCheckerTests
{
    public class CheckAll
    {
        [Fact]
        public async Task WithNoDeadLetterMessages_DoesNotLogWarning()
        {
            var client = new FakeMessageBusClient { DeadLetterMessageCountToReturn = 0 };
            var (checker, logger) = BuildChecker(client, [new DeadLetterIngestionSettings("orders", "azure-service-bus")], []);

            await checker.CheckAll(CancellationToken.None);

            Assert.DoesNotContain(logger.Messages, m => m.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task WithMessagesPendingAndNoPriorIngestion_LogsWarning()
        {
            var client = new FakeMessageBusClient { DeadLetterMessageCountToReturn = 3 };
            var repository = new FakeDeadLetterRepository { LastIngestionTimeToReturn = null };
            var (checker, logger) = BuildChecker(client, [new DeadLetterIngestionSettings("orders", "azure-service-bus")], [], repository);

            await checker.CheckAll(CancellationToken.None);

            Assert.Contains(logger.Messages, m => m.Level == LogLevel.Warning && m.Message.Contains("orders"));
        }

        [Fact]
        public async Task WithMessagesPendingAndRecentIngestion_DoesNotLogWarning()
        {
            var client = new FakeMessageBusClient { DeadLetterMessageCountToReturn = 3 };
            var repository = new FakeDeadLetterRepository { LastIngestionTimeToReturn = DateTimeOffset.UtcNow };
            var (checker, logger) = BuildChecker(
                client,
                [new DeadLetterIngestionSettings("orders", "azure-service-bus")],
                [],
                repository,
                healthSettings: new DeadLetterIngestionHealthSettings(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));

            await checker.CheckAll(CancellationToken.None);

            Assert.DoesNotContain(logger.Messages, m => m.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task WithMessagesPendingAndStaleIngestion_LogsWarning()
        {
            var client = new FakeMessageBusClient { DeadLetterMessageCountToReturn = 3 };
            var repository = new FakeDeadLetterRepository { LastIngestionTimeToReturn = DateTimeOffset.UtcNow.AddHours(-1) };
            var (checker, logger) = BuildChecker(
                client,
                [new DeadLetterIngestionSettings("orders", "azure-service-bus")],
                [],
                repository,
                healthSettings: new DeadLetterIngestionHealthSettings(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));

            await checker.CheckAll(CancellationToken.None);

            Assert.Contains(logger.Messages, m => m.Level == LogLevel.Warning && m.Message.Contains("orders"));
        }

        [Fact]
        public async Task WithEventSubscriptionAndMessagesPendingAndNoPriorIngestion_LogsWarningWithDisplayName()
        {
            var client = new FakeMessageBusClient { EventSubscriptionDeadLetterMessageCountToReturn = 2 };
            var repository = new FakeDeadLetterRepository { LastIngestionTimeForSubscriptionToReturn = null };
            var (checker, logger) = BuildChecker(
                client,
                [],
                [new EventSubscriptionDeadLetterIngestionSettings("order-placed", "notification-handler", "azure-service-bus")],
                repository);

            await checker.CheckAll(CancellationToken.None);

            Assert.Contains(logger.Messages, m => m.Level == LogLevel.Warning && m.Message.Contains("order-placed/notification-handler"));
        }

        [Fact]
        public async Task WithEventSubscriptionOnNonEventCapableClient_DoesNotThrowOrLog()
        {
            var client = new NonEventCapableFakeMessageBusClient();
            var (checker, logger) = BuildChecker(
                client,
                [],
                [new EventSubscriptionDeadLetterIngestionSettings("order-placed", "notification-handler", "azure-service-bus")]);

            await checker.CheckAll(CancellationToken.None);

            Assert.Empty(logger.Messages);
        }

        [Fact]
        public async Task WithNoSettings_DoesNothing()
        {
            var (checker, logger) = BuildChecker(new FakeMessageBusClient(), [], []);

            await checker.CheckAll(CancellationToken.None);

            Assert.Empty(logger.Messages);
        }
    }

    private static (DeadLetterIngestionHealthChecker Checker, FakeLogger<DeadLetterIngestionHealthChecker> Logger) BuildChecker(
        IMessageBusClient client,
        IEnumerable<DeadLetterIngestionSettings> queueSettings,
        IEnumerable<EventSubscriptionDeadLetterIngestionSettings> eventSubscriptionSettings,
        FakeDeadLetterRepository? repository = null,
        DeadLetterIngestionHealthSettings? healthSettings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterRepository>(repository ?? new FakeDeadLetterRepository());
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var logger = new FakeLogger<DeadLetterIngestionHealthChecker>();

        var checker = new DeadLetterIngestionHealthChecker(
            new FakeMessageBusClientRegistry(client),
            queueSettings,
            eventSubscriptionSettings,
            healthSettings ?? new DeadLetterIngestionHealthSettings(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)),
            scopeFactory,
            logger);

        return (checker, logger);
    }

    private sealed class NonEventCapableFakeMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "fake-non-event";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
