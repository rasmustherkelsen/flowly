using Flowly.MessageInfrastructure.BackgroundServices;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.InMemory.Tests;

public class InMemoryPartitionedStreamStatesTests
{
    private static (PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderPlacedHandler> Service, StreamPartitionStateRegistry Registry) CreateService()
    {
        var options = new InMemoryOptions();
        var manifest = new StreamQueueManifest();
        manifest.MarkAsStream("orders", null, null, partitionCount: 3);
        var clientRegistry = new MessageBusClientRegistry();
        clientRegistry.Register("in-memory", new InMemoryMessageBusClient(new InMemoryBroker(options, manifest), options), null);
        var serviceProvider = new ServiceCollection().AddScoped<OrderPlacedHandler>().BuildServiceProvider();
        var stateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);

        var service = new PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderPlacedHandler>(
            clientRegistry,
            new PartitionedMessageStreamHandlerSettings<OrderPlaced, OrderPlacedHandler>(
                "orders", "in-memory", nameof(OrderPlacedHandler), nameof(OrderPlacedHandler), 3, StartPosition.First(), 1, TimeSpan.FromSeconds(30), 0, 0),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PartitionedMessageStreamProcessingBackgroundService<OrderPlaced, OrderPlacedHandler>>.Instance,
            new NullHandlerInstrumentation(),
            stateRegistry);

        return (service, stateRegistry);
    }

    private static async Task WaitUntilPartitionsAreRecorded(StreamPartitionStateRegistry stateRegistry)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (stateRegistry.GetAll().Count < 3)
        {
            await Task.Yield();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    public class StartAsync
    {
        [Fact]
        public async Task RecordsEveryPartitionAsRunning()
        {
            var (service, stateRegistry) = CreateService();

            await service.StartAsync(CancellationToken.None);
            await WaitUntilPartitionsAreRecorded(stateRegistry);

            var statuses = stateRegistry.GetAll();
            Assert.Equal([0, 1, 2], statuses.Select(status => status.Key.Partition!.Value).Order());
            Assert.All(statuses, status => Assert.Equal(StreamPartitionState.Running, status.State));
            Assert.All(statuses, status => Assert.Equal("orders", status.Key.QueueName));

            service.Dispose();
        }
    }

    private sealed record OrderPlaced(string Value);

    private sealed class OrderPlacedHandler : MessageStreamHandler<OrderPlaced>
    {
        public override Task Handle(IMessageStreamContext<OrderPlaced> messageContext) => Task.CompletedTask;
    }
}
