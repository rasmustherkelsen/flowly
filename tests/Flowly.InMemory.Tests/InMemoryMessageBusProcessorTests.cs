using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryMessageBusProcessorTests
{
    private static (Channel<InMemoryEnvelope> main, Channel<InMemoryEnvelope> dlq, InMemoryMessageBusProcessor<TestMessage> processor) CreateProcessor(int maxConcurrentCalls = 1)
    {
        var main = Channel.CreateUnbounded<InMemoryEnvelope>();
        var dlq = Channel.CreateUnbounded<InMemoryEnvelope>();
        var options = new MessageBusProcessorOptions(maxConcurrentCalls, MessageBusReceiveMode.PeekLock);
        var processor = new InMemoryMessageBusProcessor<TestMessage>(main, dlq, "test-queue", options);
        return (main, dlq, processor);
    }

    private static InMemoryEnvelope CreateEnvelope(string value)
        => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], DateTimeOffset.UtcNow);

    public class StartProcessingMessages
    {
        [Fact]
        public async Task InvokesProcessMessageHandlerForEachMessage()
        {
            var (main, _, processor) = CreateProcessor();
            var received = new List<string>();

            processor.ProcessMessage += (msg, _) =>
            {
                received.Add(msg.Body.Value);
                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            await main.Writer.WriteAsync(CreateEnvelope("hello"));
            await main.Writer.WriteAsync(CreateEnvelope("world"));

            await WaitUntil(() => received.Count == 2);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["hello", "world"], received);
        }

        [Fact]
        public async Task InvokesProcessErrorHandlerOnBrokerShutdown()
        {
            var (main, _, processor) = CreateProcessor();
            var errors = new List<Exception>();

            processor.ProcessError += details =>
            {
                errors.Add(details.Exception);
                return Task.CompletedTask;
            };

            processor.ProcessMessage += (_, _) => throw new InvalidOperationException("handler failure");

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            await main.Writer.WriteAsync(CreateEnvelope("x"));

            await WaitUntil(() => errors.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Single(errors);
        }
    }

    public class DeadLetter
    {
        [Fact]
        public async Task MovesMessageToDeadLetterChannel()
        {
            var (main, dlq, processor) = CreateProcessor();

            processor.ProcessMessage += async (msg, ct) =>
            {
                await msg.DeadLetter("test-reason", ct);
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            await main.Writer.WriteAsync(CreateEnvelope("x"));

            await WaitUntil(() => dlq.Reader.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.True(dlq.Reader.TryRead(out var dlqEnvelope));
            Assert.Equal("test-reason", dlqEnvelope!.ApplicationProperties[InMemoryConstants.DeadLetterReasonKey]);
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
