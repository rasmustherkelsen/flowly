using Flowly;
using Messages;

namespace Receiver.Handlers;

[StreamStartPosition(StreamStartPositionKind.First)]
internal class SensorReadingHandler(ILogger<SensorReadingHandler> logger) : MessageStreamHandler<SensorReadingMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.ConsumerName = "sample-receiver";
        options.MaxMessagesBeforeProcessing = 10;
        options.MaxWaitTime = TimeSpan.FromSeconds(2);
    }

    public override Task Handle(IMessageStreamContext<SensorReadingMessage> messageContext)
    {
        foreach (var reading in messageContext.Messages)
        {
            logger.LogInformation(
                "Received reading {Sequence} of {SensorId}: {Celsius:F1} C (partition {Partition})",
                reading.Sequence,
                reading.SensorId,
                reading.Celsius,
                messageContext.Partition);
        }

        return Task.CompletedTask;
    }
}
