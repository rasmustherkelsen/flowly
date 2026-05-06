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

    private sealed class TestTopologyNameResolver : ITopologyNameResolver
    {
        public string ResolveQueueName<TMessage>() => "test-queue";
        public string ResolveEventName<TEvent>() => "test-topic";
        public string ResolveSubscriptionName<THandler>() => "test-subscription";
    }
}
