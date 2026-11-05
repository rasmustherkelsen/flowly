namespace Flowly.MessageInfrastructure.HandlerSteps;

internal interface IHandlerStepRunner
{
    Task Run(
        object message,
        MessageProperties properties,
        Type handlerType,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken,
        HandlerStepDelegate handler);
}
