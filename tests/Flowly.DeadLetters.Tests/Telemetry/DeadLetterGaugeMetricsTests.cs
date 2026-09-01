using System.Diagnostics.Metrics;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Tests.Telemetry;

public class DeadLetterGaugeMetricsTests
{
    [Collection(TelemetryTestCollection.Name)]
    public class UpdatePendingCount
    {
        [Fact]
        public void WithTelemetryEnabled_PublishesGaugeValue()
        {
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

            metrics.UpdatePendingCount(42);
            listener.RecordObservableInstruments();

            Assert.Contains(42, values);
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotThrow()
        {
            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            metrics.UpdatePendingCount(42);
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotPublishAnyInstrument()
        {
            var instrumentsSeen = new List<string>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                    instrumentsSeen.Add(instrument.Name);
            };
            listener.Start();

            using var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            Assert.DoesNotContain(FlowlyInstrumentationConstants.DeadLettersPending, instrumentsSeen);
        }

        [Fact]
        public void UpdatingTwice_ReportsMostRecentValue()
        {
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

            metrics.UpdatePendingCount(1);
            metrics.UpdatePendingCount(7);
            listener.RecordObservableInstruments();

            Assert.Equal(7, values[^1]);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class Dispose
    {
        [Fact]
        public void WithTelemetryEnabled_DoesNotThrow()
        {
            var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            metrics.Dispose();
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotThrow()
        {
            var metrics = new DeadLetterGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            metrics.Dispose();
        }
    }
}
