namespace Flowly.InMemory.Tests;

public class InMemoryStreamProcessorTests
{
    private static InMemoryStreamProcessor<TestMessage> CreateProcessor(InMemoryStreamLog log, long startOffset)
        => new(log, startOffset, "test-stream");

    private static InMemoryEnvelope CreateEnvelope(string value)
        => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], DateTimeOffset.UtcNow);

    public class StartProcessingMessages
    {
        [Fact]
        public async Task InvokesProcessMessageHandlerForEachAppendedMessage()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            var processor = CreateProcessor(log, startOffset: 0);
            var received = new List<string>();

            processor.ProcessMessage += (msg, _) =>
            {
                lock (received)
                {
                    received.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            log.Append(CreateEnvelope("hello"));
            log.Append(CreateEnvelope("world"));

            await WaitUntil(() => received.Count == 2);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["hello", "world"], received);
        }

        [Fact]
        public async Task DeliversEntriesAlreadyInTheLogBeforeStarting()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope("already-there"));

            var processor = CreateProcessor(log, startOffset: log.ResolveStartOffset(StartPosition.First()));
            var received = new List<string>();

            processor.ProcessMessage += (msg, _) =>
            {
                lock (received)
                {
                    received.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            await WaitUntil(() => received.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["already-there"], received);
        }

        [Fact]
        public async Task IndependentReaders_EachGetTheirOwnFullReplayFromTheirOwnStartPosition()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope("first"));
            log.Append(CreateEnvelope("second"));

            var readerFromStart = CreateProcessor(log, startOffset: log.ResolveStartOffset(StartPosition.First()));
            var readerFromLast = CreateProcessor(log, startOffset: log.ResolveStartOffset(StartPosition.Last()));
            var receivedFromStart = new List<string>();
            var receivedFromLast = new List<string>();

            readerFromStart.ProcessMessage += (msg, _) =>
            {
                lock (receivedFromStart)
                {
                    receivedFromStart.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };
            readerFromLast.ProcessMessage += (msg, _) =>
            {
                lock (receivedFromLast)
                {
                    receivedFromLast.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await readerFromStart.StartProcessingMessages(cts.Token);
            await readerFromLast.StartProcessingMessages(cts.Token);

            log.Append(CreateEnvelope("third"));

            await WaitUntil(() => receivedFromStart.Count == 3);
            await WaitUntil(() => receivedFromLast.Count == 1);
            await readerFromStart.StopProcessing(CancellationToken.None);
            await readerFromLast.StopProcessing(CancellationToken.None);

            Assert.Equal(["first", "second", "third"], receivedFromStart);
            Assert.Equal(["third"], receivedFromLast);
        }

        [Fact]
        public async Task InvokesProcessErrorHandlerWhenMessageHandlerThrows()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            var processor = CreateProcessor(log, startOffset: 0);
            var errors = new List<Exception>();

            processor.ProcessError += details =>
            {
                lock (errors)
                {
                    errors.Add(details.Exception);
                }

                return Task.CompletedTask;
            };
            processor.ProcessMessage += (_, _) => throw new InvalidOperationException("handler failure");

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            log.Append(CreateEnvelope("x"));

            await WaitUntil(() => errors.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Single(errors);
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task ThrowsNotSupportedException()
        {
            var envelope = CreateEnvelope("x");
            var received = new InMemoryStreamReceivedMessage<TestMessage>(envelope);

            await Assert.ThrowsAsync<NotSupportedException>(() => received.DeadLetter());
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(condition(), "Condition was not met within timeout.");
    }

    private record TestMessage(string Value);
}
