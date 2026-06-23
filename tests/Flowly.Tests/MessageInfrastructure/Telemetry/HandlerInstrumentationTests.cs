using System.Diagnostics;
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

            using var handlerInstrumentation = new HandlerInstrumentation();
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

            using var handlerInstrumentation = new HandlerInstrumentation();
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

            using var handlerInstrumentation = new HandlerInstrumentation();
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

            using var handlerInstrumentation = new HandlerInstrumentation();
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

            using var handlerInstrumentation = new HandlerInstrumentation();
            handlerInstrumentation.RecordReceived("MyHandler", "my-queue", 5);

            Assert.Equal(5, recorded);
        }

        [Fact]
        public void RecordResponseSent_IncrementsRepliedCounterAndRecordsDuration()
        {
            using var meterListener = new MeterListener();
            long? repliedCount = null;
            double? duration = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name is FlowlyInstrumentationConstants.CallHandlerReplied
                    or FlowlyInstrumentationConstants.CallHandlerReplyDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => repliedCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation();
            handlerInstrumentation.RecordResponseSent("ping", 33.0);

            Assert.Equal(1, repliedCount);
            Assert.Equal(33.0, duration);
        }

        [Fact]
        public void RecordResponseFailed_IncrementsReplyFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.CallHandlerReplyFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation();
            handlerInstrumentation.RecordResponseFailed("ping");

            Assert.Equal(1, recorded);
        }
    }

    public class WhenDisabled
    {
        [Fact]
        public void IsEnabled_ReturnsFalse()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            Assert.False(handlerInstrumentation.IsEnabled);
        }

        [Fact]
        public void StartHandling_ReturnsNull()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "fake", MessageProperties.Empty);
            Assert.Null(activity);
        }

        [Fact]
        public void RecordReceived_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordReceived("MyHandler", "my-queue");
        }

        [Fact]
        public void RecordSucceeded_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordSucceeded("MyHandler", "my-queue", 100.0);
        }

        [Fact]
        public void RecordFailed_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordFailed("MyHandler", "my-queue");
        }

        [Fact]
        public void RecordRetried_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordRetried("MyHandler", "my-queue");
        }

        [Fact]
        public void StartSendingResponse_ReturnsNull()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            var activity = handlerInstrumentation.StartSendingResponse("ping", "ping-reply-sender", "fake", "msg-1", "corr-1");
            Assert.Null(activity);
        }

        [Fact]
        public void RecordResponseSent_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordResponseSent("ping", 33.0);
        }

        [Fact]
        public void RecordResponseFailed_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordResponseFailed("ping");
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
                Sample = (ref _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            var traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded, isRemote: true);
            var messageProperties = new MessageProperties("msg-123", "corr-456");

            using var handlerInstrumentation = new HandlerInstrumentation();
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
                Sample = (ref _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            using var handlerInstrumentation = new HandlerInstrumentation();
            using var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "azure_service_bus", MessageProperties.Empty);

            Assert.NotNull(activity);
            Assert.Equal(default, activity.ParentSpanId);
        }
    }
}