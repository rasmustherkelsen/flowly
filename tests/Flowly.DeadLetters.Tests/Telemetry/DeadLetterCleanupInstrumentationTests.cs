using System.Diagnostics.Metrics;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Tests.Telemetry;

public class DeadLetterCleanupInstrumentationTests
{
    [Collection(TelemetryTestCollection.Name)]
    public class RecordPendingPurged
    {
        [Fact]
        public void PublishesPendingPurgedCounter()
        {
            var values = new List<long>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.DeadLetterCleanupPendingPurged)
                    l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => values.Add(measurement));
            listener.Start();
            using var instrumentation = new DeadLetterCleanupInstrumentation();

            instrumentation.RecordPendingPurged(5);

            Assert.Contains(5, values);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class RecordRequeuedPurged
    {
        [Fact]
        public void PublishesRequeuedPurgedCounter()
        {
            var values = new List<long>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.DeadLetterCleanupRequeuedPurged)
                    l.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => values.Add(measurement));
            listener.Start();
            using var instrumentation = new DeadLetterCleanupInstrumentation();

            instrumentation.RecordRequeuedPurged(3);

            Assert.Contains(3, values);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class Dispose
    {
        [Fact]
        public void DoesNotThrow()
        {
            var instrumentation = new DeadLetterCleanupInstrumentation();

            instrumentation.Dispose();
        }
    }
}
