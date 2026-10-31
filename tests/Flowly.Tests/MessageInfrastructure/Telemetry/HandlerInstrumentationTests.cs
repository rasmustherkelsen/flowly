using Flowly.MessageInfrastructure.StreamPartitions;
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            handlerInstrumentation.RecordRetried("MyHandler", "my-queue");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordHalted_IncrementsHaltedCounter()
        {
            using var meterListener = new MeterListener();
            long? recorded = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.HandlerMessagesHalted)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => recorded = value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            handlerInstrumentation.RecordHalted("MyHandler", "my-queue", "boom");

            Assert.Equal(1, recorded);
        }

        [Fact]
        public void RecordHalted_SetsErrorStatusAndHaltedEventOnCurrentActivity()
        {
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
                Sample = (ref _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(activityListener);

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            using var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "rabbitmq", MessageProperties.Empty);

            handlerInstrumentation.RecordHalted("MyHandler", "my-queue", "boom");

            Assert.NotNull(activity);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("halted", activity.GetTagItem("outcome"));
            var haltedEvent = Assert.Single(activity.Events, e => e.Name == "flowly.stream.halted");
            Assert.Contains(haltedEvent.Tags, tag => tag.Key == "reason" && Equals(tag.Value, "boom"));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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
        public void RecordHalted_DoesNotThrow()
        {
            var handlerInstrumentation = new NullHandlerInstrumentation();
            handlerInstrumentation.RecordHalted("MyHandler", "my-queue", "boom");
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
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

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            using var activity = handlerInstrumentation.StartHandling("MyHandler", "my-queue", "azure_service_bus", MessageProperties.Empty);

            Assert.NotNull(activity);
            Assert.Equal(default, activity.ParentSpanId);
        }
    }

    public class StreamPartitionMetrics
    {
        private static readonly StreamPartitionKey PartitionTwo = new("orders", "OrderConsumer", 2);

        [Fact]
        public void RecordStreamPartitionTransition_IncrementsTheTransitionCounterTaggedWithQueueConsumerPartitionAndState()
        {
            using var meterListener = new MeterListener();
            var recorded = new List<(long Value, Dictionary<string, object?> Tags)>();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionTransitions)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, value, tags, _) => recorded.Add((value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            handlerInstrumentation.RecordStreamPartitionTransition("OrderHandler", "orders", "OrderConsumer", 2, StreamPartitionState.Halted);

            var (value, tags) = Assert.Single(recorded);
            Assert.Equal(1, value);
            Assert.Equal("OrderHandler", tags["handler"]);
            Assert.Equal("orders", tags[FlowlyInstrumentationConstants.MessagingDestinationName]);
            Assert.Equal("OrderConsumer", tags["consumer"]);
            Assert.Equal(2, tags["partition"]);
            Assert.Equal("halted", tags["state"]);
        }

        [Fact]
        public void RecordStreamPartitionTransition_TagsRunningInLowerCase() => Assert.Equal("running", RecordedStateTag(StreamPartitionState.Running));

        [Fact]
        public void RecordStreamPartitionTransition_TagsWaitingInLowerCase() => Assert.Equal("waiting", RecordedStateTag(StreamPartitionState.Waiting));

        [Fact]
        public void RecordStreamPartitionTransition_TagsHaltedInLowerCase() => Assert.Equal("halted", RecordedStateTag(StreamPartitionState.Halted));

        [Fact]
        public void RecordStreamPartitionTransition_ForANonPartitionedStream_OmitsThePartitionTag()
        {
            using var meterListener = new MeterListener();
            string[]? tagKeys = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionTransitions)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => tagKeys = tags.ToArray().Select(tag => tag.Key).ToArray());
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            handlerInstrumentation.RecordStreamPartitionTransition("OrderHandler", "orders", "OrderConsumer", null, StreamPartitionState.Running);

            Assert.DoesNotContain("partition", tagKeys!);
        }

        [Fact]
        public void StateGauge_ReportsTheCurrentStateOfEachOwnedPartition()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            registry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderConsumer", 3), StreamPartitionState.Running, null);
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderConsumer", 4), StreamPartitionState.Waiting, new TimeoutException("store down"));

            var observed = ObserveStateGauge(registry);

            Assert.Equal(2, observed[2]);
            Assert.Equal(0, observed[3]);
            Assert.Equal(1, observed[4]);
        }

        [Fact]
        public void StateGauge_TagsEachMeasurementWithHandlerQueueConsumerAndPartition()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            registry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);
            using var meterListener = new MeterListener();
            Dictionary<string, object?>? recordedTags = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionCurrentState)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<int>((_, _, tags, _) => recordedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
            meterListener.Start();
            using var handlerInstrumentation = new HandlerInstrumentation(registry);

            meterListener.RecordObservableInstruments();

            Assert.Equal("OrderHandler", recordedTags!["handler"]);
            Assert.Equal("orders", recordedTags[FlowlyInstrumentationConstants.MessagingDestinationName]);
            Assert.Equal("OrderConsumer", recordedTags["consumer"]);
            Assert.Equal(2, recordedTags["partition"]);
            Assert.False(recordedTags.ContainsKey("state"));
        }

        [Fact]
        public void StateGauge_ForANonPartitionedStream_OmitsThePartitionTag()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            registry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderConsumer", null), StreamPartitionState.Running, null);
            using var meterListener = new MeterListener();
            string[]? tagKeys = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionCurrentState)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<int>((_, _, tags, _) => tagKeys = tags.ToArray().Select(tag => tag.Key).ToArray());
            meterListener.Start();
            using var handlerInstrumentation = new HandlerInstrumentation(registry);

            meterListener.RecordObservableInstruments();

            Assert.DoesNotContain("partition", tagKeys!);
        }

        [Fact]
        public void StateGauge_StopsReportingARevokedPartition()
        {
            var registry = new StreamPartitionStateRegistry(TimeProvider.System);
            registry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            registry.Remove(PartitionTwo);

            Assert.Empty(ObserveStateGauge(registry));
        }

        private static object? RecordedStateTag(StreamPartitionState state)
        {
            using var meterListener = new MeterListener();
            object? stateTag = null;
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionTransitions)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => stateTag = tags.ToArray().Single(tag => tag.Key == "state").Value);
            meterListener.Start();

            using var handlerInstrumentation = new HandlerInstrumentation(new StreamPartitionStateRegistry(TimeProvider.System));
            handlerInstrumentation.RecordStreamPartitionTransition("OrderHandler", "orders", "OrderConsumer", 2, state);

            return stateTag;
        }

        private static Dictionary<int, int> ObserveStateGauge(StreamPartitionStateRegistry registry)
        {
            using var meterListener = new MeterListener();
            var observed = new Dictionary<int, int>();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == FlowlyInstrumentationConstants.StreamPartitionCurrentState)
                    listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<int>((_, value, tags, _) =>
                observed[(int)tags.ToArray().Single(tag => tag.Key == "partition").Value!] = value);
            meterListener.Start();
            using var handlerInstrumentation = new HandlerInstrumentation(registry);

            meterListener.RecordObservableInstruments();

            return observed;
        }
    }
}
