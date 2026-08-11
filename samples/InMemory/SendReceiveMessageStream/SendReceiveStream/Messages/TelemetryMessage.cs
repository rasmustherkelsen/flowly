using Flowly;

namespace SendReceiveStream.Messages;

[StreamRetention(maxAgeSeconds: 3600, maxLengthBytes: 500_000)]
internal record TelemetryMessage(long MessageNumber);