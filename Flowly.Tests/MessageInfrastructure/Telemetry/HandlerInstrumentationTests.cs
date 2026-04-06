using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class HandlerInstrumentationTests
{
    public class WhenEnabled
    {
        [Fact]
        public void RecordReceived_IncrementsReceivedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.HandlerMessagesReceived)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            handlerInstrumentation.RecordReceived("MyHandler", "my-queue");

            meterListener.RecordObservableInstruments();

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordSucceeded_IncrementsSucceededCounterAndRecordsDuration()
        {
            using var meterListener = new MeterListener();
            long? succeededCount = null;
            double? duration = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name is FlowlyInstrumentationConstants.HandlerMessagesSucceeded
                    or FlowlyInstrumentationConstants.HandlerProcessingDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => succeededCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            handlerInstrumentation.RecordSucceeded("MyHandler", "my-queue", 123.4);

            Assert.Equal(1, succeededCount);
            Assert.Equal(123.4, duration);
        }

        [Fact]
        public void RecordFailed_IncrementsFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.HandlerMessagesFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            handlerInstrumentation.RecordFailed("MyHandler", "my-queue");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordRetried_IncrementsRetriedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.HandlerMessagesRetried)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            handlerInstrumentation.RecordRetried("MyHandler", "my-queue");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordReceived_WithCount_IncrementsCounterByCount()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.HandlerMessagesReceived)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            handlerInstrumentation.RecordReceived("MyHandler", "my-queue", count: 5);

            Assert.Equal(5, recorded);
        }
    }

    public class WhenDisabled
    {
        [Fact]
        public void NoMeterIsCreated()
        {
            using var handlerInstrumentation = new HandlerInstrumentation(false);
            Assert.False(handlerInstrumentation.IsEnabled);
        }

        [Fact]
        public void StartHandling_ReturnsNull()
        {
            using var handlerInstrumentation = new HandlerInstrumentation(false);
            var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue");
            Assert.Null(activity);
        }
    }
}
