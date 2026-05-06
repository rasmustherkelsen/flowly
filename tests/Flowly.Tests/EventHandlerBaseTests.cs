namespace Flowly.Tests;

public class EventHandlerBaseTests
{
    public class Handle
    {
        [Fact]
        public async Task OverriddenImplementation_IsInvokedWithEventContext()
        {
            var recordingEventHandler = new RecordingEventHandler();
            var eventContext = new StubEventContext(new TestEvent("payload"));

            await recordingEventHandler.Handle(eventContext, CancellationToken.None);

            Assert.Same(eventContext, recordingEventHandler.ReceivedContext);
        }

        [Fact]
        public async Task OverriddenImplementation_ReceivesCancellationToken()
        {
            var recordingEventHandler = new RecordingEventHandler();
            var eventContext = new StubEventContext(new TestEvent("payload"));
            using var cancellationTokenSource = new CancellationTokenSource();

            await recordingEventHandler.Handle(eventContext, cancellationTokenSource.Token);

            Assert.Equal(cancellationTokenSource.Token, recordingEventHandler.ReceivedCancellationToken);
        }

        [Fact]
        public async Task ThrowingImplementation_PropagatesException()
        {
            var throwingEventHandler = new ThrowingEventHandler();
            var eventContext = new StubEventContext(new TestEvent("payload"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => throwingEventHandler.Handle(eventContext, CancellationToken.None));
        }
    }

    private record TestEvent(string Payload);

    private class RecordingEventHandler : EventHandlerBase<TestEvent>
    {
        public IEventContext<TestEvent>? ReceivedContext { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public override Task Handle(IEventContext<TestEvent> eventContext, CancellationToken cancellationToken)
        {
            ReceivedContext = eventContext;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private class ThrowingEventHandler : EventHandlerBase<TestEvent>
    {
        public override Task Handle(IEventContext<TestEvent> eventContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    private sealed class StubEventContext(TestEvent eventBody) : IEventContext<TestEvent>
    {
        public TestEvent Event { get; } = eventBody;
        public string MessageId => "msg-1";
        public string? CorrelationId => null;
        public DateTimeOffset EnqueuedAt => DateTimeOffset.UtcNow;
    }
}
