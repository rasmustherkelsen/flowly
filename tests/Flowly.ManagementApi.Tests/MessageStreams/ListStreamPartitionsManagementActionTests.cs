using Flowly.ManagementApi.MessageStreams;
using Flowly.ManagementApi.Tests.Fakes;
using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flowly.ManagementApi.Tests.MessageStreams;

public class ListStreamPartitionsManagementActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

    public class Properties
    {
        [Fact]
        public void FeatureAreaIsMessageStreams()
        {
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)).StateRegistry);

            Assert.Equal("message-streams", listStreamPartitionsManagementAction.FeatureArea);
        }

        [Fact]
        public void RequiresTheReadCapability()
        {
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)).StateRegistry);

            Assert.Equal(ManagementCapability.Read, listStreamPartitionsManagementAction.RequiredCapability);
        }
    }

    public class List
    {
        [Fact]
        public void WithoutPartitions_ReturnsAnEmptyList()
        {
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)).StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, null, null));

            Assert.Empty(result.Value!);
        }

        [Fact]
        public void ReturnsEveryPartitionOrderedByQueueConsumerAndPartition()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 2, StreamPartitionState.Running);
            host.Add("invoices", "InvoiceConsumer", null, StreamPartitionState.Running);
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, null, null));

            Assert.Equal(["invoices:", "orders:0", "orders:2"], result.Value!.Select(partition => $"{partition.QueueName}:{partition.Partition}"));
        }

        [Fact]
        public void MapsTheStatusIncludingTheLastError()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, null, null));

            var partition = Assert.Single(result.Value!);
            Assert.Equal(
                new StreamPartitionResponse(
                    FakeStreamPartitionHost.HandlerName,
                    "orders",
                    "OrderConsumer",
                    1,
                    "Halted",
                    Now,
                    new StreamPartitionErrorResponse("poison", typeof(InvalidOperationException).FullName!, Now)),
                partition);
        }

        [Fact]
        public void ForAPartitionThatNeverFailed_ReturnsNoLastError()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Running);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, null, null));

            Assert.Null(Assert.Single(result.Value!).LastError);
        }

        [Fact]
        public void WithAQueueFilter_ReturnsOnlyThatQueue()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            host.Add("invoices", "InvoiceConsumer", 0, StreamPartitionState.Running);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List("invoices", null, null, null));

            Assert.Equal("invoices", Assert.Single(result.Value!).QueueName);
        }

        [Fact]
        public void WithAConsumerFilter_ReturnsOnlyThatConsumer()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            host.Add("orders", "AuditConsumer", 0, StreamPartitionState.Running);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, "AuditConsumer", null, null));

            Assert.Equal("AuditConsumer", Assert.Single(result.Value!).ConsumerName);
        }

        [Fact]
        public void WithAPartitionFilter_ReturnsOnlyThatPartition()
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Running);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, 1, null));

            Assert.Equal(1, Assert.Single(result.Value!).Partition);
        }

        [Theory]
        [InlineData("Halted")]
        [InlineData("halted")]
        [InlineData("HALTED")]
        public void WithAStateFilter_ReturnsOnlyThatStateIgnoringCase(string state)
        {
            var host = new FakeStreamPartitionHost(new FixedTimeProvider(Now));
            host.Add("orders", "OrderConsumer", 0, StreamPartitionState.Running);
            host.Add("orders", "OrderConsumer", 1, StreamPartitionState.Halted);
            host.Add("orders", "OrderConsumer", 2, StreamPartitionState.Waiting);
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(host.StateRegistry);

            var result = Assert.IsType<Ok<List<StreamPartitionResponse>>>(listStreamPartitionsManagementAction.List(null, null, null, state));

            Assert.Equal(1, Assert.Single(result.Value!).Partition);
        }

        [Theory]
        [InlineData("Stuck")]
        [InlineData("7")]
        public void WithAnUnknownState_ReturnsBadRequest(string state)
        {
            var listStreamPartitionsManagementAction = new ListStreamPartitionsManagementAction(new FakeStreamPartitionHost(new FixedTimeProvider(Now)).StateRegistry);

            var result = Assert.IsType<ProblemHttpResult>(listStreamPartitionsManagementAction.List(null, null, null, state));

            Assert.Equal(400, result.StatusCode);
        }
    }
}
