using Flowly;

namespace App.Messages;

public record MyReturnMessage(string Reply);

public record MyMessage(string Text) : IReturns<MyReturnMessage>;
