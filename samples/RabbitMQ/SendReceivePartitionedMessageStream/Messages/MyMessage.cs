using Flowly;

namespace Messages;

[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]
[StreamPartitions(2)]
public record MyMessage(string Text);
