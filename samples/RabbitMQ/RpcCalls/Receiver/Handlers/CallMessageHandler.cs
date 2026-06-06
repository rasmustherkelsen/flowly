using Flowly;
using Messages;

namespace Receiver.Handlers;

internal class CallMessageHandler : CallHandler<CallMessage, ReturnMessage>
{
    protected override Task<ReturnMessage> Handle(IMessageContext<CallMessage> messageContext)
    {
        return Task.FromResult(new ReturnMessage($"Received: {messageContext.Message.Payload}"));
    }
}