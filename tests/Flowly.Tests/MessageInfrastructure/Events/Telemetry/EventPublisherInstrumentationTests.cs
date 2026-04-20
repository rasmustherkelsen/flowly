using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Events.Telemetry;

public class EventPublisherInstrumentationTests
{
    public class WhenEnabled
    {
        [Fact]
        public void RecordRaised_IncrementsRaisedCounterAndRecordsDuration()
        {
            using var meterListener = new MeterListener();
            long? raisedCount = null;
            double? duration = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name is FlowlyInstrumentationConstants.EventPublisherEventsRaised
                    or FlowlyInstrumentationConstants.EventPublisherRaiseDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => raisedCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var eventPublisherInstrumentation = new EventPublisherInstrumentation();
            eventPublisherInstrumentation.RecordRaised("order-placed", 15.5);

            Assert.Equal(1, raisedCount);
            Assert.Equal(15.5, duration);
        }

        [Fact]
        public void RecordFailed_IncrementsFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.EventPublisherEventsFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var eventPublisherInstrumentation = new EventPublisherInstrumentation();
            eventPublisherInstrumentation.RecordFailed("order-placed");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void IsEnabled_ReturnsTrue()
        {
            using var eventPublisherInstrumentation = new EventPublisherInstrumentation();

            Assert.True(eventPublisherInstrumentation.IsEnabled);
        }
    }

    public class StartRaising
    {
        [Fact]
        public void ReturnsActivityWhenListenersAttached()
        {
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            using var eventPublisherInstrumentation = new EventPublisherInstrumentation();
            using var activity = eventPublisherInstrumentation.StartRaising("order-placed", "fake-bus", "msg-1");

            Assert.NotNull(activity);
            Assert.Equal(ActivityKind.Producer, activity.Kind);
        }
    }

    public class Dispose
    {
        [Fact]
        public void DoesNotThrow()
        {
            var eventPublisherInstrumentation = new EventPublisherInstrumentation();

            eventPublisherInstrumentation.Dispose();
        }
    }

    public class NullImplementation
    {
        [Fact]
        public void IsEnabled_ReturnsFalse()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            Assert.False(nullEventPublisherInstrumentation.IsEnabled);
        }

        [Fact]
        public void StartRaising_ReturnsNull()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            var activity = nullEventPublisherInstrumentation.StartRaising("topic", "fake", "msg");

            Assert.Null(activity);
        }

        [Fact]
        public void RecordRaised_DoesNotThrow()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            nullEventPublisherInstrumentation.RecordRaised("topic", 0);
        }

        [Fact]
        public void RecordFailed_DoesNotThrow()
        {
            var nullEventPublisherInstrumentation = new NullEventPublisherInstrumentation();

            nullEventPublisherInstrumentation.RecordFailed("topic");
        }
    }
}
