using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.InMemory.Tests;

public class InMemoryPartitionedShutdownTests
{
    private const int PartitionCount = 3;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private static (PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderHandler> BackgroundService, StreamPartitionStateRegistry StateRegistry) Create()
    {
        var options = new InMemoryOptions();
        var manifest = new StreamQueueManifest();
        manifest.MarkAsStream("orders", null, null, PartitionCount);
        var clientRegistry = new MessageBusClientRegistry();
        clientRegistry.Register("in-memory", new InMemoryMessageBusClient(new InMemoryBroker(options, manifest), options), null);
        var serviceProvider = new ServiceCollection().AddScoped<OrderHandler>().BuildServiceProvider();
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);

        var backgroundService = new PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderHandler>(
            clientRegistry,
            new PartitionedMessageStreamHandlerSettings<OrderPlaced, OrderHandler>(
                "orders", "in-memory", nameof(OrderHandler), nameof(OrderHandler), PartitionCount, StartPosition.First(), 1, TimeSpan.FromSeconds(30), 0, 0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderHandler>>.Instance,
            new NullHandlerInstrumentation(),
            stateRegistry,
            new StreamPartitionResumeRegistry());

        return (backgroundService, stateRegistry);
    }

    public class StopAsync
    {
        [Fact]
        public async Task WithAllPartitionsAssigned_StopsCleanly()
        {
            var (backgroundService, stateRegistry) = Create();
            await backgroundService.StartAsync(CancellationToken.None);
            await WaitUntilAllPartitionsRunning(stateRegistry);

            await backgroundService.StopAsync(CancellationToken.None);

            Assert.Empty(stateRegistry.GetAll());
        }
    }

    private static async Task WaitUntilAllPartitionsRunning(StreamPartitionStateRegistry stateRegistry)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);

        while (Enumerable.Range(0, PartitionCount).Any(partition => stateRegistry.Get(new StreamPartitionKey("orders", nameof(OrderHandler), partition))?.State != StreamPartitionState.Running))
        {
            await Task.Yield();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    private sealed record OrderPlaced(string Value);

    private sealed class OrderHandler : MessageStreamHandler<OrderPlaced>
    {
        public override Task Handle(IMessageStreamContext<OrderPlaced> messageContext) => Task.CompletedTask;
    }
}
