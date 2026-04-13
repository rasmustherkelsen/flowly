using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class SubmitterInstrumentationTests
{
    public class WhenEnabled
    {
        [Fact]
        public void RecordSent_IncrementsSentCounterAndRecordsDuration()
        {
            using var meterListener = new MeterListener();
            long? sentCount = null;
            double? duration = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name is FlowlyInstrumentationConstants.SubmitterMessagesSent
                    or FlowlyInstrumentationConstants.SubmitterSendDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => sentCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var submitterInstrumentation = new SubmitterInstrumentation(true);
            submitterInstrumentation.RecordSent("my-queue", 42.0);

            Assert.Equal(1, sentCount);
            Assert.Equal(42.0, duration);
        }

        [Fact]
        public void RecordFailed_IncrementsFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.SubmitterMessagesFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var submitterInstrumentation = new SubmitterInstrumentation(true);
            submitterInstrumentation.RecordFailed("my-queue");

            Assert.Equal(1, recorded);
        }
    }

    public class WhenDisabled
    {
        [Fact]
        public void NoMeterIsCreated()
        {
            using var submitterInstrumentation = new SubmitterInstrumentation(false);
            Assert.False(submitterInstrumentation.IsEnabled);
        }

        [Fact]
        public void StartSending_ReturnsNull()
        {
            using var submitterInstrumentation = new SubmitterInstrumentation(false);
            var activity = submitterInstrumentation.StartSending("my-queue", "fake", "msg-123");
            Assert.Null(activity);
        }
    }
}
