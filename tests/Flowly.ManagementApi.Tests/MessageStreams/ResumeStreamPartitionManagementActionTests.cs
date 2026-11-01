using Flowly.ManagementApi.MessageStreams;
using Flowly.ManagementApi.Tests.Fakes;
using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowly.ManagementApi.Tests.MessageStreams;

public class ResumeStreamPartitionManagementActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

    private static ResumeStreamPartitionManagementAction CreateAction(FakeStreamPartitionHost host) => new(host.StateRegistry, host.ResumeRegistry);

    public class Properties
    {
        [Fact]
        public void FeatureAreaIsMessageStreams()
        {
            var resumeStreamPartitionManagementAction = CreateAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)));

            Assert.Equal("message-streams", resumeStreamPartitionManagementAction.FeatureArea);
        }

        [Fact]
        public void RequiresTheWriteCapability()
        {
            var resumeStreamPartitionManagementAction = CreateAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)));

            Assert.Equal(ManagementCapability.Write, resumeStreamPartitionManagementAction.RequiredCapability);
        }
    }

    public class Resume
    {
        [Fact]
        public void ForAHaltedPartition_ResumesItAndReturnsNoContent()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            var partition = host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 1));

            Assert.IsType<NoContent>(result);
            Assert.Equal(1, partition.ResumeCalls);
        }

        [Fact]
        public void ForAHaltedNonPartitionedStream_AddressesTheNullPartition()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            var partition = host.Add("orders", "OrderConsumer", null, StreamPartitionState.Halted);
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", null));

            Assert.IsType<NoContent>(result);
            Assert.Equal(1, partition.ResumeCalls);
        }

        [Fact]
        public void OnlyResumesTheAddressedPartition()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Halted);
            var other = host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted);
            var resumeStreamPartitionManagementAction = CreateAction(host);

            resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 0));

            Assert.Equal(0, other.ResumeCalls);
        }

        [Fact]
        public void ForAPartitionThisInstanceDoesNotOwn_ReturnsNotFound()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Halted);
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 5)));

            Assert.Equal(404, result.StatusCode);
            Assert.Contains("partition 5", result.ProblemDetails.Detail);
        }

        [Fact]
        public void ForARunningPartition_ReturnsConflictWithoutResuming()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            var partition = host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 0)));

            Assert.Equal(409, result.StatusCode);
            Assert.Equal(0, partition.ResumeCalls);
        }

        [Fact]
        public void ForAWaitingPartition_ReturnsConflictExplainingItIsRetriedAutomatically()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Waiting, new TimeoutException("store down"));
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 0)));

            Assert.Equal(409, result.StatusCode);
            Assert.Contains("retried automatically", result.ProblemDetails.Detail);
        }

        [Fact]
        public void WhenThePartitionStoppedBeingHaltedBeforeTheResume_ReturnsConflict()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            var key = new StreamPartitionKey("orders", "OrderConsumer", 0);
            host.StateRegistry.Set(FakeStreamPartitionHost.HandlerName, key, StreamPartitionState.Halted, null);
            var registryWhereItIsNotHalted = new StreamPartitionStateRegistry(new FixedTimeProvider(Now));
            host.ResumeRegistry.Register(key, new FakeResumableStreamPartition(registryWhereItIsNotHalted, FakeStreamPartitionHost.HandlerName, key));
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 0)));

            Assert.Equal(409, result.StatusCode);
        }

        [Fact]
        public void WhenThePartitionLeftBeforeTheResume_ReturnsNotFound()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.StateRegistry.Set(FakeStreamPartitionHost.HandlerName, new StreamPartitionKey("orders", "OrderConsumer", 0), StreamPartitionState.Halted, null);
            var resumeStreamPartitionManagementAction = CreateAction(host);

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest("orders", "OrderConsumer", 0)));

            Assert.Equal(404, result.StatusCode);
        }

        [Theory]
        [InlineData("", "OrderConsumer")]
        [InlineData("orders", "")]
        [InlineData(" ", "OrderConsumer")]
        public void WithoutQueueOrConsumer_ReturnsBadRequest(string queueName, string consumerName)
        {
            var resumeStreamPartitionManagementAction = CreateAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)));

            var result = Assert.IsType<ProblemHttpResult>(resumeStreamPartitionManagementAction.Resume(new ResumeStreamPartitionRequest(queueName, consumerName, 0)));

            Assert.Equal(400, result.StatusCode);
        }
    }
}
