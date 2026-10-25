using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsPartitionProcessorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    public class Dispatch
    {
        [Fact]
        public async Task RaisesProcessMessageForEveryEventInOrder()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var bodies = new List<string>();
            eventHubsPartitionProcessor.ProcessMessage += (message, _) =>
            {
                bodies.Add(message.Body);

                return Task.CompletedTask;
            };

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0, "a"), TestEvents.Create(1, "b"), TestEvents.Create(2, "c")], CancellationToken.None);

            Assert.Equal(["a", "b", "c"], bodies);
        }

        [Fact]
        public async Task DeliversTheStreamOffsetOfEveryEvent()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var offsets = new List<long?>();
            eventHubsPartitionProcessor.ProcessMessage += (message, _) =>
            {
                offsets.Add(message.Properties.StreamOffset);

                return Task.CompletedTask;
            };

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(7), TestEvents.Create(8)], CancellationToken.None);

            Assert.Equal<long?>([7, 8], offsets);
        }

        [Fact]
        public async Task RemembersTheSequenceNumberOfTheLastDispatchedEvent()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(7), TestEvents.Create(8)], CancellationToken.None);

            Assert.Equal(8, eventHubsPartitionProcessor.LastDispatchedSequenceNumber);
        }

        [Fact]
        public async Task RemembersSequenceNumberZero()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);

            Assert.Equal(0, eventHubsPartitionProcessor.LastDispatchedSequenceNumber);
        }

        [Fact]
        public void BeforeAnyDispatch_HasNoLastDispatchedSequenceNumber()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");

            Assert.Null(eventHubsPartitionProcessor.LastDispatchedSequenceNumber);
        }

        [Fact]
        public async Task WhenAHandlerThrows_RaisesProcessErrorAndContinuesWithTheNextEvent()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var errors = new List<ErrorDetails>();
            var bodies = new List<string>();
            eventHubsPartitionProcessor.ProcessError += errorDetails =>
            {
                errors.Add(errorDetails);

                return Task.CompletedTask;
            };
            eventHubsPartitionProcessor.ProcessMessage += (message, _) =>
            {
                bodies.Add(message.Body);

                return message.Body == "a" ? throw new InvalidOperationException("handler failed") : Task.CompletedTask;
            };

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0, "a"), TestEvents.Create(1, "b")], CancellationToken.None);

            Assert.Equal("handler failed", Assert.Single(errors).Exception.Message);
            Assert.Equal("readings", errors[0].EndPoint);
            Assert.Equal(["a", "b"], bodies);
        }

        [Fact]
        public async Task WithoutHandlers_Completes()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);
        }

        [Fact]
        public async Task AfterAHandlerIsRemoved_NoLongerCallsIt()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var calls = 0;
            Func<IReceivedMessage<string>, CancellationToken, Task> handler = (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };
            eventHubsPartitionProcessor.ProcessMessage += handler;
            eventHubsPartitionProcessor.ProcessMessage -= handler;

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task AfterStopProcessing_BlocksUntilReleasedWithoutDispatching()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var calls = 0;
            eventHubsPartitionProcessor.ProcessMessage += (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };
            await eventHubsPartitionProcessor.StopProcessing(CancellationToken.None);

            var dispatch = eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);

            Assert.False(dispatch.IsCompleted);

            eventHubsPartitionProcessor.Release();
            await dispatch.WaitAsync(TestTimeout);

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task AfterStopProcessing_ReturnsWhenTheSdkCancelsTheBatch()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            await eventHubsPartitionProcessor.StopProcessing(CancellationToken.None);
            using var cancellationTokenSource = new CancellationTokenSource();

            var dispatch = eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], cancellationTokenSource.Token);
            await cancellationTokenSource.CancelAsync();

            await dispatch.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task AfterDisposeAsync_StaysBlockedUntilReleased()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            await eventHubsPartitionProcessor.DisposeAsync();

            var dispatch = eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);

            Assert.False(dispatch.IsCompleted);

            eventHubsPartitionProcessor.Release();
            await dispatch.WaitAsync(TestTimeout);
        }

        [Fact]
        public async Task AfterReleaseWithoutHalt_StillDispatches()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            var calls = 0;
            eventHubsPartitionProcessor.ProcessMessage += (_, _) =>
            {
                calls++;

                return Task.CompletedTask;
            };
            eventHubsPartitionProcessor.Release();

            await eventHubsPartitionProcessor.Dispatch([TestEvents.Create(0)], CancellationToken.None);

            Assert.Equal(1, calls);
        }
    }

    public class RaiseError
    {
        [Fact]
        public async Task RaisesProcessErrorWithTheEventHubAsTheEndpoint()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");
            ErrorDetails? received = null;
            eventHubsPartitionProcessor.ProcessError += errorDetails =>
            {
                received = errorDetails;

                return Task.CompletedTask;
            };
            var exception = new InvalidOperationException("boom");

            await eventHubsPartitionProcessor.RaiseError(exception);

            Assert.Same(exception, received!.Exception);
            Assert.Equal("readings", received.EndPoint);
        }
    }

    public class StartProcessingMessages
    {
        [Fact]
        public async Task Completes()
        {
            var eventHubsPartitionProcessor = new EventHubsPartitionProcessor<string>("readings");

            await eventHubsPartitionProcessor.StartProcessingMessages();
        }
    }
}
