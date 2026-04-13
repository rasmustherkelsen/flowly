using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.MessagingAbstractions;

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
            var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "fake", MessageProperties.Empty);
            Assert.Null(activity);
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
            var messageProperties = new MessageProperties("msg-123", "corr-456");

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            using var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "azure_service_bus", messageProperties, parentContext);

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

            using var handlerInstrumentation = new HandlerInstrumentation(true);
            using var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "azure_service_bus", MessageProperties.Empty);

            Assert.NotNull(activity);
            Assert.Equal(default, activity.ParentSpanId);
        }
    }
}
