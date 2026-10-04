using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.DeadLetters.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.DeadLetters.Tests.Telemetry;

public class DeadLetterOperationInstrumentationTests
{
    private static ActivityListener StartListeningToActivities()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FlowlyInstrumentationConstants.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Collection(TelemetryTestCollection.Name)]
    public class StartRequeue
    {
        [Fact]
        public void CreatesActivityNamedForTheQueue()
        {
            using var listener = StartListeningToActivities();
            using var instrumentation = new DeadLetterOperationInstrumentation();

            using var activity = instrumentation.StartRequeue("orders", "msg-1", default);

            Assert.NotNull(activity);
            Assert.Equal("flowly.deadletter.requeue orders", activity.DisplayName);
        }

        [Fact]
        public void TagsActivityWithQueueNameAndMessageId()
        {
            using var listener = StartListeningToActivities();
            using var instrumentation = new DeadLetterOperationInstrumentation();

            using var activity = instrumentation.StartRequeue("orders", "msg-1", default);

            Assert.Equal("orders", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingDestinationName));
            Assert.Equal("msg-1", activity?.GetTagItem(FlowlyInstrumentationConstants.MessagingMessageId));
        }

        [Fact]
        public void WithOriginalContext_LinksToIt()
        {
            using var listener = StartListeningToActivities();
            using var instrumentation = new DeadLetterOperationInstrumentation();
            var originalContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            using var activity = instrumentation.StartRequeue("orders", "msg-1", originalContext);

            Assert.Contains(activity!.Links, link => link.Context.TraceId == originalContext.TraceId);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class StartDiscard
    {
        [Fact]
        public void CreatesActivityNamedForTheQueue()
        {
            using var listener = StartListeningToActivities();
            using var instrumentation = new DeadLetterOperationInstrumentation();

            using var activity = instrumentation.StartDiscard("orders", "msg-1", default);

            Assert.NotNull(activity);
            Assert.Equal("flowly.deadletter.discard orders", activity.DisplayName);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class RecordRequeued
    {
        [Fact]
        public void PublishesRequeuedCounter()
        {
            var measurements = new List<(long Value, string QueueName)>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.DeadLettersRequeued)
                    l.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
                measurements.Add((measurement, tags.ToArray().First(t => t.Key == FlowlyInstrumentationConstants.MessagingDestinationName).Value!.ToString()!)));
            meterListener.Start();
            using var instrumentation = new DeadLetterOperationInstrumentation();

            instrumentation.RecordRequeued("orders");

            Assert.Contains(measurements, m => m.Value == 1 && m.QueueName == "orders");
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class RecordDiscarded
    {
        private static List<string> RecordAndCaptureReasonTags(DeadLetterDiscardReason reason)
        {
            var reasons = new List<string>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.DeadLettersDiscarded)
                    l.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
                reasons.Add(tags.ToArray().First(t => t.Key == "reason").Value!.ToString()!));
            meterListener.Start();
            using var instrumentation = new DeadLetterOperationInstrumentation();

            instrumentation.RecordDiscarded("orders", reason);

            return reasons;
        }

        [Fact]
        public void WithUserInitiatedReason_TagsAsUserInitiated()
        {
            var reasons = RecordAndCaptureReasonTags(DeadLetterDiscardReason.UserInitiated);

            Assert.Contains("user_initiated", reasons);
        }

        [Fact]
        public void WithExpiredReason_TagsAsExpired()
        {
            var reasons = RecordAndCaptureReasonTags(DeadLetterDiscardReason.Expired);

            Assert.Contains("expired", reasons);
        }
    }

    [Collection(TelemetryTestCollection.Name)]
    public class Dispose
    {
        [Fact]
        public void DoesNotThrow()
        {
            var instrumentation = new DeadLetterOperationInstrumentation();

            instrumentation.Dispose();
        }
    }
}
