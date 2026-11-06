using Flowly.MessageInfrastructure;

namespace Flowly.Tests;

public class FlowlyOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void CreateTopologyDefaultsToTrue()
        {
            var flowlyOptions = new FlowlyOptions();

            Assert.True(flowlyOptions.CreateTopology);
        }

        [Fact]
        public void EnableTelemetryDefaultsToTrue()
        {
            var flowlyOptions = new FlowlyOptions();

            Assert.True(flowlyOptions.EnableTelemetry);
        }

        [Fact]
        public void TopologyNameResolverDefaultsToKebabCase()
        {
            var flowlyOptions = new FlowlyOptions();

            Assert.Equal(typeof(KebabCaseTopologyNameResolver), flowlyOptions.TopologyNameResolverType);
        }
    }

    public class Setters
    {
        [Fact]
        public void CreateTopologyCanBeSetToFalse()
        {
            var flowlyOptions = new FlowlyOptions { CreateTopology = false };

            Assert.False(flowlyOptions.CreateTopology);
        }

        [Fact]
        public void EnableTelemetryCanBeSetToFalse()
        {
            var flowlyOptions = new FlowlyOptions { EnableTelemetry = false };

            Assert.False(flowlyOptions.EnableTelemetry);
        }
    }

    public class WithTopologyNameResolver
    {
        [Fact]
        public void SetsResolverTypeToCustomResolver()
        {
            var flowlyOptions = new FlowlyOptions();

            flowlyOptions.WithTopologyNameResolver<TestTopologyNameResolver>();

            Assert.Equal(typeof(TestTopologyNameResolver), flowlyOptions.TopologyNameResolverType);
        }

        [Fact]
        public void CalledTwice_LastOneWins()
        {
            var flowlyOptions = new FlowlyOptions();

            flowlyOptions.WithTopologyNameResolver<TestTopologyNameResolver>();
            flowlyOptions.WithTopologyNameResolver<KebabCaseTopologyNameResolver>();

            Assert.Equal(typeof(KebabCaseTopologyNameResolver), flowlyOptions.TopologyNameResolverType);
        }
    }

    public class NonRetryable
    {
        [Fact]
        public void RegistersExceptionType()
        {
            var flowlyOptions = new FlowlyOptions();

            flowlyOptions.NonRetryable<InvalidOperationException>();

            Assert.Equal([typeof(InvalidOperationException)], flowlyOptions.NonRetryableExceptionTypes);
        }

        [Fact]
        public void CalledForSeveralTypes_RegistersAll()
        {
            var flowlyOptions = new FlowlyOptions();

            flowlyOptions.NonRetryable<InvalidOperationException>();
            flowlyOptions.NonRetryable<UnauthorizedAccessException>();

            Assert.Equal([typeof(InvalidOperationException), typeof(UnauthorizedAccessException)], flowlyOptions.NonRetryableExceptionTypes);
        }

        [Fact]
        public void CalledTwiceForSameType_RegistersItOnce()
        {
            var flowlyOptions = new FlowlyOptions();

            flowlyOptions.NonRetryable<InvalidOperationException>();
            flowlyOptions.NonRetryable<InvalidOperationException>();

            Assert.Single(flowlyOptions.NonRetryableExceptionTypes);
        }

        [Fact]
        public void DefaultsToNoRegisteredTypes()
        {
            var flowlyOptions = new FlowlyOptions();

            Assert.Empty(flowlyOptions.NonRetryableExceptionTypes);
        }
    }

    private sealed class TestTopologyNameResolver : ITopologyNameResolver
    {
        public string ResolveQueueName<TMessage>() => "test-queue";
        public string ResolveEventName<TEvent>() => "test-topic";
        public string ResolveSubscriptionName<THandler>() => "test-subscription";
        public string ResolveReplyQueueName(string callQueueName, string instanceName) => $"{callQueueName}-reply-{instanceName}";
    }
}
