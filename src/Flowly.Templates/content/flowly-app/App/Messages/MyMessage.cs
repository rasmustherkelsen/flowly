#if (UseCallHandler || UseStream)
using Flowly;

#endif
namespace App.Messages;

#if (UseCallHandler)
public record MyReturnMessage(string Reply);

public record MyMessage(string Text) : IReturns<MyReturnMessage>;
#else
#if (UseStream)
[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]
#if (UseStreamPartitions)
[StreamPartitions(424242)]
#endif
#endif
public record MyMessage(string Text);
#endif
