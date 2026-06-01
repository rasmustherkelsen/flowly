using Flowly;

namespace Messages;

public record ReturnMessage(string ReturnValue);

public record CallMessage(string Payload) : IReturns<ReturnMessage>;
