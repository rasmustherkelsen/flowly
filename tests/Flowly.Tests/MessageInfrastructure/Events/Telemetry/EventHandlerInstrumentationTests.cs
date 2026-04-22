using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Events.Telemetry;

public class EventHandlerInstrumentationTests
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
                if (instrument.Name == FlowlyInstrumentationConstants.EventHandlerMessagesReceived)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            eventHandlerInstrumentation.RecordReceived("MyHandler", "order-placed");

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
                if (instrument.Name is FlowlyInstrumentationConstants.EventHandlerMessagesSucceeded
                    or FlowlyInstrumentationConstants.EventHandlerProcessingDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => succeededCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            eventHandlerInstrumentation.RecordSucceeded("MyHandler", "order-placed", 42.0);

            Assert.Equal(1, succeededCount);
            Assert.Equal(42.0, duration);
        }

        [Fact]
        public void RecordFailed_IncrementsFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.EventHandlerMessagesFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            eventHandlerInstrumentation.RecordFailed("MyHandler", "order-placed");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordRetried_IncrementsRetriedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.EventHandlerMessagesRetried)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            eventHandlerInstrumentation.RecordRetried("MyHandler", "order-placed");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordReceived_WithCount_IncrementsCounterByCount()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.EventHandlerMessagesReceived)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            eventHandlerInstrumentation.RecordReceived("MyHandler", "order-placed", count: 7);

            Assert.Equal(7, recorded);
        }

        [Fact]
        public void IsEnabled_ReturnsTrue()
        {
            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();

            Assert.True(eventHandlerInstrumentation.IsEnabled);
        }
    }

    public class StartHandling
    {
        [Fact]
        public void WithParentContext_CreatesChildSpan()
        {
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            var traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded, isRemote: true);

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            using var activity = eventHandlerInstrumentation.StartHandling(
                "MyHandler",
                "order-placed",
                "fake-bus",
                new MessageProperties("msg-1", "corr-1"),
                parentContext);

            Assert.NotNull(activity);
            Assert.Equal(traceId, activity.TraceId);
            Assert.Equal(parentSpanId, activity.ParentSpanId);
        }

        [Fact]
        public void WithoutParentContext_CreatesRootSpan()
        {
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            using var eventHandlerInstrumentation = new EventHandlerInstrumentation();
            using var activity = eventHandlerInstrumentation.StartHandling("MyHandler", "order-placed", "fake-bus", MessageProperties.Empty);

            Assert.NotNull(activity);
            Assert.Equal(default, activity.ParentSpanId);
        }
    }

    public class Dispose
    {
        [Fact]
        public void DoesNotThrow()
        {
            var eventHandlerInstrumentation = new EventHandlerInstrumentation();

            eventHandlerInstrumentation.Dispose();
        }
    }

    public class NullImplementation
    {
        [Fact]
        public void IsEnabled_ReturnsFalse()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            Assert.False(nullEventHandlerInstrumentation.IsEnabled);
        }

        [Fact]
        public void StartHandling_ReturnsNull()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            var activity = nullEventHandlerInstrumentation.StartHandling("h", "topic", "fake", MessageProperties.Empty);

            Assert.Null(activity);
        }

        [Fact]
        public void RecordReceived_DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordReceived("h", "topic");
        }

        [Fact]
        public void RecordSucceeded_DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordSucceeded("h", "topic", 10.0);
        }

        [Fact]
        public void RecordFailed_DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordFailed("h", "topic");
        }

        [Fact]
        public void RecordRetried_DoesNotThrow()
        {
            var nullEventHandlerInstrumentation = new NullEventHandlerInstrumentation();

            nullEventHandlerInstrumentation.RecordRetried("h", "topic");
        }
    }
}
