using Flowly;
using Flowly.InMemory;
using SendReceiveStream.Handlers;
using SendReceiveStream.Messages;

namespace SendReceiveStream;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseInMemory();
        builder.AddMessageRecorder<TelemetryMessage>();
        builder.AddMessageStreamHandler<TelemetryMessage, TelemetryMessageStreamHandlerOne>();
        builder.AddMessageStreamHandler<TelemetryMessage, TelemetryMessageStreamHandlerTwo>();
    }
}
