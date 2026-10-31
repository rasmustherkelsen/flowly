using System.Threading.Channels;
using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.InMemory.Tests;

public class InMemoryHaltedPartitionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly StreamPartitionKey Key = new("orders", nameof(FlakyHandler), 0);

    private static Harness Create(Func<string, bool> fails)
    {
        var options = new InMemoryOptions();
        var manifest = new StreamQueueManifest();
        manifest.MarkAsStream("orders", null, null, partitionCount: 2);
        var broker = new InMemoryBroker(options, manifest);
        var clientRegistry = new MessageBusClientRegistry();
        clientRegistry.Register("in-memory", new InMemoryMessageBusClient(broker, options), null);
        var handler = new FlakyHandler(fails);
        var serviceProvider = new ServiceCollection().AddScoped<FlakyHandler>(_ => handler).BuildServiceProvider();
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);
        var resumeRegistry = new StreamPartitionResumeRegistry();

        var service = new PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, FlakyHandler>(
            clientRegistry,
            new PartitionedMessageStreamHandlerSettings<OrderPlaced, FlakyHandler>(
                "orders", "in-memory", nameof(FlakyHandler), nameof(FlakyHandler), 2, StartPosition.First(), 1, TimeSpan.FromSeconds(30), 0, 0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, FlakyHandler>>.Instance,
            new NullHandlerInstrumentation(),
            stateRegistry,
            resumeRegistry);

        return new Harness(service, broker, handler, stateRegistry, resumeRegistry);
    }

    public class ResumeHaltedPartition
    {
        [Fact]
        public async Task HoldsTheFailedBatchWithoutLosingMessagesAndContinuesContiguouslyAfterResume()
        {
            var failing = true;
            var harness = Create(value => value == "poison" && Volatile.Read(ref failing));
            await harness.Service.StartAsync(CancellationToken.None);
            var log = harness.Broker.GetOrCreatePartitionedStreamLog("orders", 0);
            log.Append(Envelope("m0"));
            log.Append(Envelope("poison"));
            for (var number = 2; number < 50; number++)
            {
                log.Append(Envelope($"m{number}"));
            }

            await harness.Handler.WaitForFailure();
            await WaitUntilHalted(harness.StateRegistry);

            Assert.Equal(StreamPartitionState.Halted, harness.StateRegistry.Get(Key)!.State);
            Assert.Equal(["m0", "poison"], harness.Handler.Handled);

            Volatile.Write(ref failing, false);
            var result = harness.ResumeRegistry.Resume(Key);
            await harness.Handler.WaitForHandled(51);

            Assert.Equal(StreamPartitionResumeResult.Resumed, result);
            Assert.Equal(["m0", "poison", "poison", .. Enumerable.Range(2, 48).Select(number => $"m{number}")], harness.Handler.Handled);
            Assert.Equal(StreamPartitionState.Running, harness.StateRegistry.Get(Key)!.State);

            harness.Service.Dispose();
        }
    }

    private static async Task WaitUntilHalted(StreamPartitionStateRegistry stateRegistry)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);

        while (stateRegistry.Get(Key)?.State != StreamPartitionState.Halted)
        {
            await Task.Yield();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    private static InMemoryEnvelope Envelope(string value) => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], DateTimeOffset.UtcNow);

    private sealed record Harness(
        PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, FlakyHandler> Service,
        InMemoryBroker Broker,
        FlakyHandler Handler,
        StreamPartitionStateRegistry StateRegistry,
        StreamPartitionResumeRegistry ResumeRegistry);

    private sealed record OrderPlaced(string Value);

    private sealed class FlakyHandler(Func<string, bool> fails) : MessageStreamHandler<OrderPlaced>
    {
        private readonly Lock _lock = new();
        private readonly List<string> _handled = [];
        private readonly Channel<bool> _failures = Channel.CreateUnbounded<bool>();
        private readonly List<(int Count, TaskCompletionSource Signal)> _waiters = [];

        public IReadOnlyList<string> Handled
        {
            get
            {
                lock (_lock)
                {
                    return [.._handled];
                }
            }
        }

        public async Task WaitForFailure() => await _failures.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);

        public Task WaitForHandled(int count)
        {
            lock (_lock)
            {
                if (_handled.Count >= count)
                    return Task.CompletedTask;

                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((count, signal));

                return signal.Task.WaitAsync(TestTimeout);
            }
        }

        public override Task Handle(IMessageStreamContext<OrderPlaced> messageContext)
        {
            var value = messageContext.Messages.Single().Value;
            List<TaskCompletionSource> toSignal;
            lock (_lock)
            {
                _handled.Add(value);
                toSignal = _waiters.Where(waiter => _handled.Count >= waiter.Count).Select(waiter => waiter.Signal).ToList();
            }

            foreach (var signal in toSignal)
            {
                signal.TrySetResult();
            }

            if (!fails(value))
                return Task.CompletedTask;

            _failures.Writer.TryWrite(true);

            throw new InvalidOperationException("poison batch");
        }
    }
}
