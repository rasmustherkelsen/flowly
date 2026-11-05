namespace Flowly.MessageInfrastructure.HandlerSteps;

internal interface IHandlerStepInvoker
{
    Task Invoke(object message, MessageProperties properties, Type handlerType, CancellationToken cancellationToken, HandlerStepDelegate next);
}
