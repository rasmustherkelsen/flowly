using System.Threading.Channels;

namespace Flowly.InMemory.Tests;

public class InMemoryExecutionLaneProcessorTests
{
    private static (Channel<InMemoryEnvelope> laneChannel, InMemoryExecutionLaneProcessor processor) CreateProcessor()
    {
        var laneChannel = Channel.CreateUnbounded<InMemoryEnvelope>();
        var processor = new InMemoryExecutionLaneProcessor(laneChannel, "recurring-jobs");
        return (laneChannel, processor);
    }

    public class StartProcessing
    {
        [Fact]
        public async Task InvokesProcessMessageHandlerForEachMessage()
        {
            var (laneChannel, processor) = CreateProcessor();
            var received = new List<string>();

            processor.ProcessMessage += (msg, _) =>
            {
                received.Add(msg.Properties.MessageId);
                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessing(cts.Token);

            await laneChannel.Writer.WriteAsync(new InMemoryEnvelope("id-1", string.Empty, [], DateTimeOffset.UtcNow));
            await laneChannel.Writer.WriteAsync(new InMemoryEnvelope("id-2", string.Empty, [], DateTimeOffset.UtcNow));

            await WaitUntil(() => received.Count == 2);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["id-1", "id-2"], received);
        }

        [Fact]
        public async Task ProcessesMessagesSequentially()
        {
            var (laneChannel, processor) = CreateProcessor();
            var concurrency = 0;
            var maxConcurrency = 0;

            processor.ProcessMessage += async (_, _) =>
            {
                Interlocked.Increment(ref concurrency);
                maxConcurrency = Math.Max(maxConcurrency, concurrency);
                await Task.Delay(20);
                Interlocked.Decrement(ref concurrency);
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessing(cts.Token);

            for (var i = 0; i < 3; i++)
                await laneChannel.Writer.WriteAsync(new InMemoryEnvelope($"id-{i}", string.Empty, [], DateTimeOffset.UtcNow));

            await WaitUntil(() => laneChannel.Reader.Count == 0, 2000);
            await Task.Delay(50);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(1, maxConcurrency);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(condition(), "Condition was not met within timeout.");
    }
}
