using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ;

/// <summary>
///     Creates the Stream protocol consumer of a Super Stream, so the partitioned stream consumer can close and recreate it without
///     depending on a broker in tests.
/// </summary>
internal interface IRabbitMqSuperStreamConsumerFactory
{
    /// <summary>
    ///     Creates and subscribes a consumer for the Super Stream.
    /// </summary>
    /// <param name="onMessage">Called with the partition stream name, the message offset and the message for each delivery.</param>
    /// <param name="onConsumerUpdate">
    ///     Called when the broker reports that the consumer became (in)active for a partition stream; the returned offset is where the
    ///     client subscribes the partition.
    /// </param>
    /// <returns>A handle whose disposal closes the consumer.</returns>
    Task<IAsyncDisposable> Create(Func<string, long, Message, Task> onMessage, Func<string, bool, Task<IOffsetType>> onConsumerUpdate);
}
