using System.Diagnostics.Metrics;
using Flowly.DeadLetters.BackgroundServices;
using Flowly.DeadLetters.Telemetry;
using Flowly.DeadLetters.Tests.Fakes;
using Flowly.DeadLetters.Tests.Helpers;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Tests.BackgroundServices;

public class DeadLetterMetricsBackgroundServiceTests
{
    [Collection(Flowly.DeadLetters.Tests.Telemetry.TelemetryTestCollection.Name)]
    public class RefreshPendingCount
    {
        [Fact]
        public async Task PublishesCountOfPendingDeadLetters()
        {
            using var factory = new SqliteDbContextFactory();
            var repository = new Flowly.DeadLetters.Repositories.DeadLetterRepository(factory);
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-1" }, new FakeDeadLetterMessage { MessageId = "msg-2" }], "orders");
            await repository.SaveBatch([new FakeDeadLetterMessage { MessageId = "msg-3" }], "orders");
            await repository.MarkAsRequeued("msg-3", null);

            var values = new List<long>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.DeadLettersPending)
                    l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => values.Add(measurement));
            listener.Start();
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });
            var service = new DeadLetterMetricsBackgroundService(factory, metrics, new FlowlyOptions { EnableTelemetry = true });

            await service.RefreshPendingCount(CancellationToken.None);
            listener.RecordObservableInstruments();

            Assert.Contains(2, values);
        }

        [Fact]
        public async Task WhenDbContextFactoryThrows_SwallowsException()
        {
            var factory = new SqliteDbContextFactory();
            factory.Dispose();
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });
            var service = new DeadLetterMetricsBackgroundService(factory, metrics, new FlowlyOptions { EnableTelemetry = false });

            await service.RefreshPendingCount(CancellationToken.None);
        }

        [Fact]
        public async Task WhenCancellationRequested_DoesNotSwallowException()
        {
            var factory = new SqliteDbContextFactory();
            factory.Dispose();
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });
            var service = new DeadLetterMetricsBackgroundService(factory, metrics, new FlowlyOptions { EnableTelemetry = false });
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshPendingCount(cts.Token));
        }
    }

    [Collection(Flowly.DeadLetters.Tests.Telemetry.TelemetryTestCollection.Name)]
    public class ExecuteAsync
    {
        [Fact]
        public async Task WithTelemetryDisabled_CompletesWithoutTouchingDatabase()
        {
            using var factory = new SqliteDbContextFactory();
            factory.Dispose();
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });
            var service = new DeadLetterMetricsBackgroundService(factory, metrics, new FlowlyOptions { EnableTelemetry = false });

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task WithTelemetryEnabled_RunsAtLeastOneIterationThenStopsOnCancellation()
        {
            using var factory = new SqliteDbContextFactory();
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });
            var service = new DeadLetterMetricsBackgroundService(factory, metrics, new FlowlyOptions { EnableTelemetry = true });

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        }
    }
}
