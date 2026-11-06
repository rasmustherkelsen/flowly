using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqSuperStreamConsumerFactory(StreamSystem streamSystem, string queueName) : IRabbitMqSuperStreamConsumerFactory
{
    public async Task<IAsyncDisposable> Create(Func<string, long, Message, Task> onMessage, Func<string, bool, Task<IOffsetType>> onConsumerUpdate)
        => new ConsumerHandle(await Consumer.Create(new ConsumerConfig(streamSystem, queueName)
        {
            IsSuperStream = true,
            IsSingleActiveConsumer = true,
            Reference = queueName,
            MessageHandler = (sourceStream, _, messageContext, message) => onMessage(sourceStream, (long)messageContext.Offset, message),
            ConsumerUpdateListener = (_, sourceStream, isActive) => onConsumerUpdate(sourceStream, isActive)
        }));

    private sealed class ConsumerHandle(Consumer consumer) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await consumer.Close();
    }
}
