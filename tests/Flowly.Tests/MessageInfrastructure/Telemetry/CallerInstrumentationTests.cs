using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class CallerInstrumentationTests
{
    public class WhenEnabled
    {
        [Fact]
        public void StartCalling_CreatesActivityWithCorrectNameAndKind()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartCalling("ping", "fake-bus", "msg-1", "corr-1");

            Assert.NotNull(activity);
            Assert.Equal("flowly.call ping", activity.OperationName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
        }

        [Fact]
        public void StartCalling_SetsMessagingSystemTag()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartCalling("ping", "azure-service-bus", "msg-1", "corr-1");

            Assert.Equal("azure-service-bus", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingSystem));
        }

        [Fact]
        public void StartCalling_SetsDestinationNameTag()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartCalling("ping", "fake", "msg-1", "corr-1");

            Assert.Equal("ping", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingDestinationName));
        }

        [Fact]
        public void StartCalling_SetsMessageIdAndCorrelationIdTags()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartCalling("ping", "fake", "msg-abc", "corr-xyz");

            Assert.Equal("msg-abc", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingMessageId));
            Assert.Equal("corr-xyz", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingMessageConversationId));
        }

        [Fact]
        public void RecordSucceeded_IncrementsSucceededCounterAndRecordsDuration()
        {
            using var meterListener = new MeterListener();
            long? succeededCount = null;
            double? duration = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name is FlowlyInstrumentationConstants.CallerCallsSucceeded
                    or FlowlyInstrumentationConstants.CallerCallDuration)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => succeededCount = value);
            meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => duration = value);
            meterListener.Start();

            using var callerInstrumentation = new CallerInstrumentation();
            callerInstrumentation.RecordSucceeded("ping", 77.5);

            Assert.Equal(1, succeededCount);
            Assert.Equal(77.5, duration);
        }

        [Fact]
        public void RecordFailed_IncrementsFailedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.CallerCallsFailed)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var callerInstrumentation = new CallerInstrumentation();
            callerInstrumentation.RecordFailed("ping");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void StartReceivingResponse_CreatesConsumerSpanWithCorrectNameAndKind()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartReceivingResponse(
                "ping-reply", "fake-bus", default, "msg-1", "corr-1");

            Assert.NotNull(activity);
            Assert.Equal("flowly.call.response ping-reply", activity.OperationName);
            Assert.Equal(ActivityKind.Consumer, activity.Kind);
        }

        [Fact]
        public void StartReceivingResponse_SetsExpectedTags()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartReceivingResponse(
                "ping-reply", "azure-service-bus", default, "msg-abc", "corr-xyz");

            Assert.Equal("azure-service-bus", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingSystem));
            Assert.Equal("ping-reply", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingDestinationName));
            Assert.Equal("receive", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingOperationType));
            Assert.Equal("msg-abc", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingMessageId));
            Assert.Equal("corr-xyz", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingMessageConversationId));
        }

        [Fact]
        public void StartReceivingResponse_UsesProvidedParentContext()
        {
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(listener);

            var traceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded, isRemote: true);

            using var callerInstrumentation = new CallerInstrumentation();
            using var activity = callerInstrumentation.StartReceivingResponse(
                "ping-reply", "fake", parentContext, "msg-1", "corr-1");

            Assert.NotNull(activity);
            Assert.Equal(traceId, activity.TraceId);
            Assert.Equal(parentSpanId, activity.ParentSpanId);
        }
    }

    public class WhenDisabled
    {
        [Fact]
        public void StartCalling_ReturnsNull()
        {
            var callerInstrumentation = new NullCallerInstrumentation();
            var activity = callerInstrumentation.StartCalling("ping", "fake", "msg-1", "corr-1");
            Assert.Null(activity);
        }

        [Fact]
        public void RecordSucceeded_DoesNotThrow()
        {
            var callerInstrumentation = new NullCallerInstrumentation();
            callerInstrumentation.RecordSucceeded("ping", 50.0);
        }

        [Fact]
        public void RecordFailed_DoesNotThrow()
        {
            var callerInstrumentation = new NullCallerInstrumentation();
            callerInstrumentation.RecordFailed("ping");
        }

        [Fact]
        public void StartReceivingResponse_ReturnsNull()
        {
            var callerInstrumentation = new NullCallerInstrumentation();
            var activity = callerInstrumentation.StartReceivingResponse("ping-reply", "fake", default, "msg-1", "corr-1");
            Assert.Null(activity);
        }
    }
}
