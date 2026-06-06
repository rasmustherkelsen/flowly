using Flowly;

namespace Messages;

public record MyReturnMessage(string Reply);

public record MyMessage(string Text) : IReturns<MyReturnMessage>;
