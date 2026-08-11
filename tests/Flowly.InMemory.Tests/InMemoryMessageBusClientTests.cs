using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryMessageBusClientTests
{
    private static InMemoryMessageBusClient CreateClient(out InMemoryBroker broker)
    {
        var options = new InMemoryOptions();
        broker = new InMemoryBroker(options);
        return new InMemoryMessageBusClient(broker, options);
    }

    public class ImplementsIStreamCapableMessageBusClient
    {
        [Fact]
        public void ClientIsStreamCapable()
        {
            var client = CreateClient(out _);

            Assert.IsAssignableFrom<IStreamCapableMessageBusClient>(client);
        }
    }

    public class CreateStreamProcessor
    {
        [Fact]
        public async Task WithFirst_DeliversMessagesAlreadyAppendedToTheLog()
        {
            var client = CreateClient(out var broker);
            broker.GetOrCreateStreamLog("orders").Append(new InMemoryEnvelope("1", """{"Value":"already-there"}""", [], DateTimeOffset.UtcNow));

            var processor = await client.CreateStreamProcessor<TestMessage>(
                "orders",
                StartPosition.First(),
                new MessageBusProcessorOptions(4, MessageBusReceiveMode.PeekLock));

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
        public async Task WithLast_SkipsMessagesAlreadyAppendedToTheLog()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(new InMemoryEnvelope("1", """{"Value":"skipped"}""", [], DateTimeOffset.UtcNow));

            var processor = await client.CreateStreamProcessor<TestMessage>(
                "orders",
                StartPosition.Last(),
                new MessageBusProcessorOptions(4, MessageBusReceiveMode.PeekLock));

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

            log.Append(new InMemoryEnvelope("2", """{"Value":"delivered"}""", [], DateTimeOffset.UtcNow));

            await WaitUntil(() => received.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["delivered"], received);
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
