using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Tests.MessageInfrastructure.Telemetry;

public class FlowlyInstrumentationConstantsTests
{
    public class MeterName
    {
        [Fact]
        public void Equals_Flowly()
        {
            Assert.Equal("Flowly", FlowlyInstrumentationConstants.MeterName);
        }
    }

    public class ActivitySourceName
    {
        [Fact]
        public void Equals_Flowly()
        {
            Assert.Equal("Flowly", FlowlyInstrumentationConstants.ActivitySourceName);
        }
    }

    public class SemanticConventionAttributes
    {
        [Fact]
        public void MessagingSystem_FollowsOtelConvention()
        {
            Assert.Equal("messaging.system", FlowlyInstrumentationConstants.MessagingSystem);
        }

        [Fact]
        public void MessagingDestinationName_FollowsOtelConvention()
        {
            Assert.Equal("messaging.destination.name", FlowlyInstrumentationConstants.MessagingDestinationName);
        }

        [Fact]
        public void MessagingOperationType_FollowsOtelConvention()
        {
            Assert.Equal("messaging.operation.type", FlowlyInstrumentationConstants.MessagingOperationType);
        }

        [Fact]
        public void MessagingMessageId_FollowsOtelConvention()
        {
            Assert.Equal("messaging.message.id", FlowlyInstrumentationConstants.MessagingMessageId);
        }

        [Fact]
        public void MessagingMessageConversationId_FollowsOtelConvention()
        {
            Assert.Equal("messaging.message.conversation_id", FlowlyInstrumentationConstants.MessagingMessageConversationId);
        }
    }

    public class HandlerMetricNames
    {
        [Fact]
        public void HandlerMessagesReceived_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.handler.received", FlowlyInstrumentationConstants.HandlerMessagesReceived);
        }

        [Fact]
        public void HandlerMessagesSucceeded_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.handler.succeeded", FlowlyInstrumentationConstants.HandlerMessagesSucceeded);
        }

        [Fact]
        public void HandlerMessagesFailed_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.handler.failed", FlowlyInstrumentationConstants.HandlerMessagesFailed);
        }

        [Fact]
        public void HandlerMessagesRetried_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.handler.retried", FlowlyInstrumentationConstants.HandlerMessagesRetried);
        }

        [Fact]
        public void HandlerProcessingDuration_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.handler.duration", FlowlyInstrumentationConstants.HandlerProcessingDuration);
        }
    }

    public class SubmitterMetricNames
    {
        [Fact]
        public void SubmitterMessagesSent_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.submitter.sent", FlowlyInstrumentationConstants.SubmitterMessagesSent);
        }

        [Fact]
        public void SubmitterMessagesFailed_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.submitter.failed", FlowlyInstrumentationConstants.SubmitterMessagesFailed);
        }

        [Fact]
        public void SubmitterSendDuration_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.message.submitter.duration", FlowlyInstrumentationConstants.SubmitterSendDuration);
        }
    }

    public class DeadLetterMetricNames
    {
        [Fact]
        public void DeadLettersPending_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.deadletter.pending", FlowlyInstrumentationConstants.DeadLettersPending);
        }
    }

    public class JobMetricNames
    {
        [Fact]
        public void JobsFailed_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.job.failed", FlowlyInstrumentationConstants.JobsFailed);
        }

        [Fact]
        public void JobsRunning_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.job.running", FlowlyInstrumentationConstants.JobsRunning);
        }
    }

    public class EventHandlerMetricNames
    {
        [Fact]
        public void EventHandlerMessagesReceived_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.handler.received", FlowlyInstrumentationConstants.EventHandlerMessagesReceived);
        }

        [Fact]
        public void EventHandlerMessagesSucceeded_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.handler.succeeded", FlowlyInstrumentationConstants.EventHandlerMessagesSucceeded);
        }

        [Fact]
        public void EventHandlerMessagesFailed_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.handler.failed", FlowlyInstrumentationConstants.EventHandlerMessagesFailed);
        }

        [Fact]
        public void EventHandlerMessagesRetried_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.handler.retried", FlowlyInstrumentationConstants.EventHandlerMessagesRetried);
        }

        [Fact]
        public void EventHandlerProcessingDuration_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.handler.duration", FlowlyInstrumentationConstants.EventHandlerProcessingDuration);
        }
    }

    public class EventPublisherMetricNames
    {
        [Fact]
        public void EventPublisherEventsRaised_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.publisher.raised", FlowlyInstrumentationConstants.EventPublisherEventsRaised);
        }

        [Fact]
        public void EventPublisherEventsFailed_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.publisher.failed", FlowlyInstrumentationConstants.EventPublisherEventsFailed);
        }

        [Fact]
        public void EventPublisherRaiseDuration_UsesFlowlyPrefix()
        {
            Assert.Equal("flowly.event.publisher.duration", FlowlyInstrumentationConstants.EventPublisherRaiseDuration);
        }
    }
}
