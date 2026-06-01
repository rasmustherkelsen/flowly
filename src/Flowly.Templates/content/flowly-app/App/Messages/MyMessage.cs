#if (UseCallHandler)
using Flowly;

#endif
namespace App.Messages;

#if (UseCallHandler)
public record MyReturnMessage(string Reply);

public record MyMessage(string Text) : IReturns<MyReturnMessage>;
#else
public record MyMessage(string Text);
#endif
